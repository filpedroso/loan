# Architecture

```
Browser ── POST /api/v1/applications ──▶ API (.NET 10) ──▶ PostgreSQL
                                          │
                                          └─ outbox worker, every second ── PUT /applications/{id} ──▶ External partner (mock)
```

The API decides right away. If it approves, it saves the customer, the loan application and an outbox message in one transaction. A worker then delivers the message to the external partner, retrying until the partner accepts it.

## Layers

| Folder | Responsibility | Depends on |
|---|---|---|
| `backend/src/Loans.Domain` | Business rules: `Customer` aggregate and its `LoanApplication`, `Ssn` and `RequestedAmount` value objects, decision rules and `DecisionEngine`, `ISsnBlocklist` port | nothing |
| `backend/src/Loans.UseCases` | The use case `SubmitLoanApplication`: validate, load, decide, save. `ICustomerRepository` port | Domain |
| `backend/src/Loans.Infrastructure` | Adapters: EF Core + PostgreSQL, repository, blocklist, SSN encryption, outbox worker, external partner client | UseCases |
| `backend/src/Loans.Api` | Thin controller, composition root, error handling, CORS, health, telemetry. Hosts the worker | all of the above |
| `partner/ExternalPartner.Mock` | The external partner, in memory. Built on its own, like another company's system | none |
| `web` | Next.js form; calls the API from the browser | the API, over HTTP |
| `infra` | Docker Compose stack and the k6 load test | the images of the above |

The use-case layer is named UseCases so that "application" always means a loan application.

Dependencies point inward, and project references enforce it: the Domain can't reference EF Core or ASP.NET. Infrastructure is a separate project, so the worker can move to its own host without the web API.

## Request flow

1. The controller maps the JSON to a command and calls `SubmitLoanApplication`.
2. The use case creates the value objects. They validate themselves; invalid input becomes a 400 that names the field.
3. `LockAndFindBySsnAsync` locks the SSN and loads the customer. A returning customer is updated (`UpdateDetails`); a new one is registered (`Register`). `customer.Apply(amount)` creates the loan application, or overwrites its amount and keeps its id.
4. `DecisionEngine` evaluates the customer. A denial returns 200 with its reason, and nothing is saved.
5. `SaveAsync` saves the customer, the loan application and an outbox message, and the API returns 200 with the loan application id. Every saved loan application is sent to the external partner, and only approvals are saved.

## Decision engine

- A rule implements `IDecisionRule`: a `Reason` and `ValueTask<bool> IsSatisfiedAsync(Customer)`. Only rules that do I/O await.
- The engine runs the rules in registration order and stops at the first failure, so a denial has one reason.
- The rules, in order:
  1. `StateServedRule`: denies the states in `Decision:BlockedStates` (`NY`). It runs first, so an applicant from such a state never has their SSN checked.
  2. `SsnNotBlocklistedRule`: denies SSNs on the blocklist, through `ISsnBlocklist`.
- Adding a rule means a new class and one registration in `Program.cs`, in the order it should run; existing rules don't change. A new denial reason also needs a `DenialReason` value, its public code in `Contracts.cs` and its text on the web's denial page.
- The applicant sees `SsnBlocklisted` as the generic `NotEligible`, so nobody is told they're on a blocklist. That's courtesy, not secrecy: approved versus denied still reveals it.

## Web app

- One page, three views: the form, a handshake animation while the API decides (shown for at least 1.2 s), and the result.
- The form checks the fields before sending (required, lengths, state, amount, SSN) and shows the API's 400 errors next to the field they name.
- Fields keep exactly what was typed. The amount accepts `50000`, `50,000` or `50000.50`; the SSN accepts `123-45-6789` or `123456789`.
- Nothing is kept in the browser. The result page shows only what was just submitted.

## Transaction and failures midway

A database write and an HTTP call can't be one atomic transaction. So the call to the external partner isn't part of the transaction; the *intent* to call it is. The outbox message is committed together with the data, and delivery is retried until it succeeds.

| Fails at | Result |
|---|---|
| Validation or decision | Nothing written |
| Any database write (customer, loan application or outbox) | One `SaveChanges` in one transaction: everything rolls back, the user gets a 500 |
| After the commit: partner down, 500, timeout | The message stays pending and is retried every second; the data is already saved |
| Partner answers 400 | Retried like any failure, then dead-lettered |
| Worker crashes mid-send, or the partner's answer is lost | The message is sent again. Harmless: `PUT` overwrites |

**Same SSN at the same moment.** Looking up the customer opens the transaction and takes a PostgreSQL advisory lock keyed on the SSN hash, held until commit or rollback. A second submission for that SSN waits a few milliseconds, then finds the customer and updates it, with no error and no retry. Different SSNs never wait for each other.

## Outbox worker

- Every second, a tick takes up to 100 pending messages, oldest first (ids are UUIDv7, so id order is creation order), and sends each one.
- The payload is built from current data at send time, one message at a time. The partner always gets the latest submission, the SSN never sits in the outbox, and one unreadable row fails alone.
- Outcomes:
  - **Any 2xx:** `delivered_at` is set.
  - **Anything else** (another status, a timeout, a network error): `attempts + 1` and a Warning log.
  - **The 10th failure:** an Error log; the message is dead-lettered (`delivered_at IS NULL AND attempts = 10`).
- Every failure costs an attempt, so an outage longer than about 10 seconds dead-letters messages. To replay them: `UPDATE outbox SET attempts = 0 WHERE delivered_at IS NULL AND attempts >= 10;`
- One worker runs. With several API instances, set `Outbox:Enabled=false` on all but one; two workers could send the same message twice, or an older payload after a newer one.
- It delivers up to 100 messages a second. A partner that hangs instead of answering makes a tick last up to 100 × 5 s.

## Calling the external partner

- `ExternalPartnerClient` is a typed `HttpClient` with a 5 s timeout and no retry policy; the outbox is the only retry mechanism.
- It always sends `PUT /applications/{id}`, which creates or overwrites, so a resend is harmless. The body holds the customer's details, SSN and requested amount. Any 2xx counts as success.
- The mock also has `POST` (create) and `GET` (list what it received), and answers 400 for invalid data.

## Personal data and security

- **SSN storage:** an HMAC-SHA256 hash for lookups and the blocklist, plus an AES-GCM encrypted copy. The copy is decrypted when a customer is loaded: for a returning customer's submission, and by the worker to send it.
- **SSN everywhere else:** `Ssn.ToString()` masks it, EF Core doesn't log parameter values, and SSNs never appear in URLs.
- **API:** CORS allows only the web origin.
- **Web app:** sends `X-Frame-Options`, `X-Content-Type-Options` and `Referrer-Policy` headers.
- **Keys:** come from configuration. The ones in `infra/docker-compose.yml` and `appsettings.Development.json` are for local use only and stay out of the published image.

## Observability

- The API sends logs, traces and metrics over OTLP to the Aspire Dashboard (http://localhost:18888) when `OTEL_EXPORTER_OTLP_ENDPOINT` is set.
- Traces cover incoming requests, calls to the external partner and the PostgreSQL commands inside a request, so a slow request shows where its time went. The worker's database commands aren't traced, because its polling would add a trace every second. Metrics include the database connection pool.
- A delivery is its own trace, separate from the request that created it. The loan application id links them: the API returns it, and the worker logs it.
- During `make load` only 1 in 10 requests gets a trace (`OTEL_TRACES_SAMPLER_ARG`), so the dashboard's own work doesn't skew the result.
- `/health` checks the database.

## Decisions and tradeoffs

| Decision | Why | Cost |
|---|---|---|
| Outbox with eventual delivery | Data and intent commit together; delivery survives partner outages | The partner is up to a second behind |
| Fixed 1 s interval, 10 attempts | Approvals reach the partner within a second; simple, no extra column | Outages longer than ~10 s dead-letter messages; replay is manual |
| One worker, one batch per tick, no row locking | No transaction or locking SQL | Up to 100 deliveries a second; several API instances need `Outbox:Enabled=false` on all but one |
| Worker inside the API process | One deployable | Scales with the API; `Outbox:Enabled=false` separates them |
| One loan application per customer, overwritten | The exercise asks for it | No history of past submissions |
| Denials aren't stored | Keeps the model small | No record of why someone was denied. In real US lending, denials need regulated notices (ECOA / Reg B) and record keeping |
| Advisory lock per SSN | No retry code, and no mixed updates | Holds a connection for the few ms between lookup and save |
| One DbContext per request, no pooling | The SSN converter needs the encryption service injected | Microseconds per request |
| Migrations at API startup | No separate migration step; EF Core locks against concurrent runs | The app's database user needs schema rights |
| Browser calls the API directly | No backend-for-frontend to build | The API is public; CORS only restricts browsers |

## Known tech debt

- No authentication; anyone can submit.
- No authentication between the API and the external partner either; in production, an API key or mTLS.
- No rate limiting; in production it belongs in the gateway in front of the API. Without it or authentication, anyone can test SSNs against the blocklist, because approved versus denied reveals it.
- Delivered outbox rows are never deleted. A cleanup job should remove old ones.
- Dead letters are only visible in logs and SQL; there's no replay tool.
- SSN key rotation isn't supported. It would need re-hashing and re-encrypting.
- The request, command and partner payload records hold the SSN as a plain string, and their generated `ToString()` prints it. No code logs them.
- The frontend has no automated tests.

## Scaling up

- Run the worker as an Azure Function on a timer trigger, calling the same `OutboxDispatcher`, with `Outbox:Enabled=false` in the API.
- Wake the worker right after a commit with PostgreSQL `LISTEN/NOTIFY` instead of polling every second.
- Space retries apart (a `next_attempt_at` column) so outages longer than ~10 s don't dead-letter messages.
- Parallel sends within a batch.
- Raise a `LoanApplicationApproved` domain event instead of sending every saved loan application, once denials are stored or a second thing reacts to an approval (an email, analytics).
- Several workers: claim messages with `FOR UPDATE SKIP LOCKED`, and keep one pending row per loan application so an older payload can't arrive after a newer one.
- Secrets in Azure Key Vault with managed identity; HTTPS terminated at the ingress.
