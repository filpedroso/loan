# Architecture

```
Browser ── POST /api/v1/applications ──▶ API (.NET 10) ──▶ PostgreSQL
                                          │
                                          └─ outbox worker, every 30 s ── PUT /applications/{id} ──▶ External partner (mock)
```

The API decides right away. If it approves, it saves the customer, the application and an outbox message in one transaction. The worker later delivers the message to the external partner and retries until the partner answers 200.

## Layers

| Folder | Responsibility | Depends on |
|---|---|---|
| `backend/src/Loans.Domain` | Business rules: `Customer` aggregate and its `LoanApplication`, `Ssn` and `RequestedAmount` value objects, decision rules and `DecisionEngine`, `ISsnBlocklist` port | nothing |
| `backend/src/Loans.UseCases` | The use case `SubmitLoanApplication`: validate, load, decide, save. `ICustomerRepository` port | Domain |
| `backend/src/Loans.Infrastructure` | Adapters: EF Core + PostgreSQL, repository, blocklist, SSN encryption, outbox worker, external partner client | Application |
| `backend/src/Loans.Api` | Thin controller, composition root, error handling, CORS, health, telemetry. Hosts the worker | all of the above |
| `partner/ExternalPartner.Mock` | The external partner, in memory. Has its own build settings, as another company's system would | none |
| `web` | Next.js form; calls the API from the browser | the API, over HTTP |
| `infra` | Docker Compose stack and the k6 load test | the images of the above |

The use-case layer is called UseCases rather than Application, so "application" always means a loan application.

Dependencies point inward, and project references enforce it: the Domain can't reference EF Core or ASP.NET. Infrastructure is a separate project, so the worker can move to its own host without the web API.

## Request flow

1. The controller maps the JSON to a command and calls `SubmitLoanApplication`.
2. The service creates the value objects. They validate themselves; invalid input becomes a 400 that names the field.
3. It loads the customer by SSN. A returning customer is updated (`UpdateDetails`); a new one is registered (`Register`). `customer.Apply(amount)` creates the application, or overwrites its amount and keeps its id.
4. `DecisionEngine` evaluates the customer. A denial returns 200 with reason codes, and nothing is saved.
5. `SaveApprovedAsync` saves customer, application and outbox message, and the API returns 200 with the application id.

## Decision engine

- A rule implements `IDecisionRule`: one `Reason` and `ValueTask<bool> IsSatisfiedAsync(Customer)`. Synchronous rules return a completed `ValueTask` at no cost; only the blocklist rule does I/O.
- The engine runs every rule without stopping at the first failure, so a denial lists all its reasons.
- Rules today:
  - `StateServedRule`: states listed in `Decision:BlockedStates`, default `NY`.
  - `SsnNotBlocklistedRule`: checks the SSN through `ISsnBlocklist`.
- To add a rule, write a class and add one line in `Program.cs`. No existing rule changes.
- The API turns `SsnBlocklisted` into the generic `NotEligible`. Otherwise anyone could test SSNs against the blocklist.

## Transaction and failures midway

A database write and an HTTP call can't be one atomic transaction. So the call to the external partner isn't part of the transaction; the *intent* to call it is. The outbox message is committed together with the data, and delivery is retried until it succeeds.

| Fails at | Result |
|---|---|
| Validation or decision | Nothing written |
| Any database write (customer, application or outbox) | One `SaveChanges` in one transaction: everything rolls back, the user gets a 500 |
| After the commit: partner down, 500, timeout | The message stays pending and is retried every 30 s; the data is already saved |
| Partner answers 400 | Retried like any failure, then dead-lettered |
| Worker crashes mid-send, or the 200 is lost | The message is sent again. Harmless: `PUT` overwrites |

**Same SSN at the same moment.** Looking up the customer opens the transaction and takes a PostgreSQL advisory lock keyed on the SSN hash, held until commit or rollback. A second submission for that SSN waits a few milliseconds, then finds the customer and updates it, with no error and no retry. Different SSNs never wait for each other.

## Outbox worker

- Every 30 s, a tick takes up to 100 pending messages, oldest first (ids are UUIDv7, so id order is creation order), and sends each one.
- The payload is built from the current data at send time, one message at a time. The partner always gets the latest submission, the SSN never sits in the outbox, and one unreadable row fails alone.
- Outcomes:
  - **Any 2xx:** `delivered_at` is set.
  - **Anything else** (another status, a timeout, a network error): `attempts + 1` and a Warning log.
  - **The 10th failure:** an Error log; the message is dead-lettered (`delivered_at IS NULL AND attempts = 10`).
- Every failure costs an attempt, so an outage longer than about 5 minutes dead-letters messages. To replay them: `UPDATE outbox SET attempts = 0 WHERE delivered_at IS NULL AND attempts >= 10;`
- One worker runs. With several API instances, set `Outbox:Enabled=false` on all but one; two workers could send the same message twice, or an older payload after a newer one.
- It delivers up to 200 messages a minute. A partner that hangs instead of answering makes a tick last up to 100 × 5 s.

## Calling the external partner

- `ExternalPartnerClient` is a typed `HttpClient` with a 5 s timeout and no retry policy; the outbox is the only retry mechanism.
- It always sends `PUT /applications/{id}`, which creates or overwrites, so a resend is harmless. The body holds the customer's details, SSN and requested amount. Any 2xx counts as success.
- The mock also has `POST` (create) and `GET` (list what it received), and answers 400 for invalid data.

## Personal data and security

- **SSN storage:** an HMAC-SHA256 hash for lookups and the blocklist, plus an AES-GCM encrypted copy that only the worker decrypts.
- **SSN everywhere else:** `Ssn.ToString()` masks it, EF Core doesn't log parameter values, and SSNs never appear in URLs.
- **Result page:** shows only what was just submitted, never stored data.
- **API:** CORS allows only the web origin.
- **Web app:** sends `X-Frame-Options`, `X-Content-Type-Options` and `Referrer-Policy` headers.
- **Keys:** come from configuration. The ones in `infra/docker-compose.yml` and `appsettings.Development.json` are for local use only and are kept out of the published image.

## Observability

- The API sends logs, traces and metrics over OTLP to the Aspire Dashboard (http://localhost:18888) when `OTEL_EXPORTER_OTLP_ENDPOINT` is set.
- Traces cover incoming requests, calls to the external partner and every PostgreSQL command, so a slow request shows where its time went. Metrics include the database connection pool.
- During `make load` only 1 in 10 requests gets a trace (`OTEL_TRACES_SAMPLER_ARG`), so the dashboard's own work doesn't skew the result.
- `/health` checks the database.
- A worker delivery is a separate trace from the request that created it; the application id in the logs connects them.

## Decisions and tradeoffs

| Decision | Why | Cost |
|---|---|---|
| Outbox with eventual delivery | Data and intent commit together; delivery survives partner outages | The partner is up to 30 s behind |
| Fixed 30 s interval, 10 attempts | Simple, and no extra column | Outages longer than ~5 min dead-letter messages; replay is manual |
| One worker, one batch per tick, no row locking | No transaction or locking SQL | Up to 200 deliveries a minute; several API instances need `Outbox:Enabled=false` on all but one |
| Worker inside the API process | One deployable | Scales with the API; `Outbox:Enabled=false` separates them |
| One application per customer, overwritten | The exercise asks for it | No history of past submissions |
| Denials aren't stored | Keeps the model small | No record of why someone was denied. In real US lending, denials need regulated notices (ECOA / Reg B) and record keeping |
| Advisory lock per SSN | No retry code, and no mixed updates | Holds a connection for the few ms between lookup and save |
| One DbContext per request, no pooling | The SSN converter needs the encryption service injected | Microseconds per request |
| Migrations at API startup | No separate migration step; EF Core locks against concurrent runs | The app's database user needs schema rights |
| Browser calls the API directly | No backend-for-frontend to build | The API is public; CORS only restricts browsers |

## Known tech debt

- No authentication; anyone can submit.
- No authentication between the API and the external partner either; in production, an API key or mTLS.
- Delivered outbox rows are never deleted. A cleanup job should remove old ones.
- Dead letters are only visible in logs and SQL; there's no replay tool.
- SSN key rotation isn't supported. It would need re-hashing and re-encrypting.
- No rate limiting; in production it belongs in the gateway in front of the API.
- The frontend has no automated tests.

## Scaling up

- Run the worker as an Azure Function on a timer trigger, calling the same `OutboxDispatcher`, with `Outbox:Enabled=false` in the API.
- Wake the worker right after a commit with PostgreSQL `LISTEN/NOTIFY` instead of waiting for the next tick.
- Exponential backoff with a `next_attempt_at` column, and parallel sends within a batch.
- Several workers: claim messages with `FOR UPDATE SKIP LOCKED`, and keep one pending row per loan application so an older payload can't arrive after a newer one.
- Secrets in Azure Key Vault with managed identity; HTTPS terminated at the ingress.
