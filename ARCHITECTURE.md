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

Dependencies point inward, and project references enforce it: the Domain can't reference EF Core or ASP.NET. The use-case layer is named UseCases so that "application" always means a loan application.

## Request flow

1. The controller maps the JSON to a command and calls `SubmitLoanApplication`.
2. `LockAndFindBySsnAsync` opens the transaction, locks the SSN (see [the lock](#transaction-and-failures-midway)) and loads the customer.
3. A returning customer is updated (`UpdateDetails`); a new one is registered (`Register`). `customer.Apply(amount)` creates the loan application, or overwrites its amount and keeps its id.
4. `DecisionEngine` evaluates the customer. A denial returns 200 with its reason, and nothing is saved.
5. On approval, `SaveAsync` saves the customer, the loan application and an outbox message, and the API returns 200 with the loan application id. Only approvals are saved, so only approvals reach the partner.

Invalid input (in the value objects or the customer's fields) throws a `ValidationException`, which becomes a 400 that names the field.

## Decision engine

- A rule implements `IDecisionRule`: a `Reason` and `ValueTask<bool> IsSatisfiedAsync(Customer)`. Only rules that do I/O await.
- The engine runs the rules in registration order and stops at the first failure, so a denial has one reason.
- The rules, in order:
  1. `StateServedRule`: denies the states in `Decision:BlockedStates` (`NY`). It runs first, so an applicant from such a state is never checked against the blocklist. (Their SSN hash is still used in step 2 of the request flow, to find and lock the customer.)
  2. `SsnNotBlocklistedRule`: denies SSNs on the blocklist, through `ISsnBlocklist`.
- **Adding a rule** means a new class and one registration in `Program.cs`, in the order it should run; existing rules don't change. A new denial reason also needs a `DenialReason` value, its public code in `Contracts.cs` and its text on the web's denial page.
- The applicant sees `SsnBlocklisted` as the generic `NotEligible`, so nobody is told they're on a blocklist. That's courtesy, not secrecy: approved versus denied still reveals it.

## Transaction and failures midway

A database write and an HTTP call can't be one atomic transaction. So the call to the external partner isn't part of the transaction; the *intent* to call it is. The outbox message is committed together with the data, and delivery is retried until it succeeds.

| Fails at | Result |
|---|---|
| Validation or decision | Nothing written |
| Any database write (customer, loan application or outbox) | One `SaveChanges` in one transaction: everything rolls back, the user gets a 500 |
| After the commit: partner down, 500, timeout | The message stays pending and is retried every second; the data is already saved |
| Partner answers 400 | Retried like any failure, then dead-lettered |
| Worker crashes mid-send, or the partner's answer is lost | The message is sent again. Harmless: `PUT` overwrites |

**The lock.** Looking up the customer takes a PostgreSQL advisory lock keyed on the SSN hash (`pg_advisory_xact_lock`). It lasts as long as the transaction: an approval releases it at commit. A denial, a validation error or a failure never commits, so the transaction rolls back when the request ends and its DbContext is disposed, and that releases the lock. A second submission for the same SSN waits until then, finds the customer and updates it, with no error and no retry. Different SSNs never wait for each other.

## Outbox worker

- Every second, a tick takes up to 100 pending messages, oldest first (ids are UUIDv7, so id order is creation order), and sends each one.
- The payload is built from current data at send time, one message at a time. The partner always gets the latest submission, the SSN never sits in the outbox, and one unreadable row fails alone.
- Outcomes:
  - **Any 2xx:** `delivered_at` is set.
  - **Anything else** (another status, a timeout, a network error): `attempts + 1` and a Warning log.
  - **The 10th failure:** an Error log; the message is dead-lettered (`delivered_at IS NULL AND attempts = 10`). To replay dead letters: `UPDATE outbox SET attempts = 0 WHERE delivered_at IS NULL AND attempts >= 10;`
- **Only one worker may run.** With several API instances, set `Outbox:Enabled=false` on all but one. Two workers could send the same message twice, or an older payload after a newer one.
- It delivers up to 100 messages a second. A partner that hangs instead of answering makes a tick last up to 100 × 5 s.

## Calling the external partner

- `ExternalPartnerClient` is a typed `HttpClient` with a 5 s timeout and no retry policy; the outbox is the only retry mechanism. Any 2xx counts as success.
- **Create vs update.** The brief says to create new customers at the partner and update returning ones. We always send `PUT /applications/{loanApplicationId}`, which creates or overwrites. This is deliberate: PUT is idempotent, so a resend after a crash or a lost answer is harmless, and nothing tracks what the partner already has. A returning customer keeps their loan application id, so their PUT overwrites the partner's copy.
- **Seeing it in the demo:** the mock answers 200 and logs the same line in both cases, so status codes and logs don't show the difference. Only http://localhost:8081/applications does: a new customer adds an entry, and a returning customer replaces theirs (same id, new data, moved to the end because `receivedAt` changes).
- **Payload:** the customer's details, SSN and requested amount. The partner's contract includes the SSN (the mock rejects a body without 9 digits), so it travels in clear in the body. Locally that's plain HTTP inside the Compose network, without authentication. In production it would use HTTPS, ideally with mTLS or an API key.

## Personal data and security

- **SSN storage:** an HMAC-SHA256 hash for lookups and the blocklist, plus an AES-GCM encrypted copy. The copy is decrypted when a customer is loaded: for a returning customer's submission, and by the worker to send it. Why both, and the cost: see [trade-offs](#trade-offs).
- **SSN everywhere else:** `Ssn.ToString()` masks it, EF Core doesn't log parameter values, and SSNs never appear in URLs.
- **API:** CORS allows only the web origin. **Web app:** sends `X-Frame-Options`, `X-Content-Type-Options` and `Referrer-Policy` headers.
- **Keys:** come from configuration. The ones in `infra/docker-compose.yml` and `appsettings.Development.json` are for local use only and stay out of the published image. In production they would come from a secret store.

## Web app

- One Next.js page: the form, then the decision. Nothing is kept in the browser.
- The form checks the fields before sending, and shows the API's 400 errors next to the field they name.

## Observability

- The API sends logs, traces and metrics over OTLP to the Aspire Dashboard (http://localhost:18888) when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. During `make load`, only 1 in 10 requests is traced.
- Logs cover every decision (a denial with its reason and the applicant's state, an approval with its loan application id), every failed delivery attempt, dead letters and successful deliveries. They never include the SSN, name or address.
- Traces cover requests, partner calls and the PostgreSQL commands inside a request. The worker's polling queries aren't traced. A delivery is its own trace, linked to its request by the loan application id, which both the approval and the delivery log.
- `/health` checks the database.

## Infrastructure (Docker)

`infra/docker-compose.yml` runs the whole app locally; `make up` builds and starts it.

- `postgres`: PostgreSQL 17, data in a named volume, with a healthcheck the API waits for.
- `api`: the .NET API (port 8080).
- `external-partner`: the mock partner (port 8081). Its `GET` endpoint lists what it received.
- `web`: the Next.js app (port 3000). The browser calls the API directly.
- `aspire-dashboard`: logs, traces and metrics, at http://localhost:18888.
- `k6`: a load test. It only runs with the `load` profile, so `make up` doesn't start it.

Services reach each other by name inside the Compose network. The database password and the SSN keys in the file are for local use only; in production they would come from a secret store.

## CI:

GitHub Actions (`.github/workflows/ci.yml`) runs on every pull request and on every push to `main`. Three independent jobs:

| Job | What it checks |
|---|---|
| `backend` | `dotnet test` over the whole solution: unit tests and integration tests. The integration tests start a real PostgreSQL with Testcontainers, using the runner's Docker, so the transactional behavior is tested against a real database. |
| `web` | `npm ci`, lint, typecheck, unit tests and a production build. |
| `images` | `docker compose build`, to catch a broken Dockerfile or compose file before someone tries to run the stack. |

The .NET and Node versions come from `backend/global.json` and `web/.nvmrc`, the same files used locally, so CI and local runs use the same versions.

## Trade-offs

What we chose, what we left out, and what we would do next.

| Decision | Why | Cost, and the way out |
|---|---|---|
| Outbox with eventual delivery | Data and intent commit together; delivery survives partner outages | The partner is up to a second behind |
| Polling every second | No extra infrastructure | A query every second even when idle. PostgreSQL `LISTEN/NOTIFY` could wake the worker right after a commit |
| Retry on every tick, 10 attempts | No scheduling code | Retries are a second apart, so a partner outage of about 10 s dead-letters messages, visible only in logs and SQL. Replay them with the [SQL above](#outbox-worker). Fix: a `next_attempt_at` column to space retries out |
| One worker, no row locking | No transaction or locking SQL | Up to 100 deliveries a second, and [only one instance may run it](#outbox-worker). Several workers would need `FOR UPDATE SKIP LOCKED` and one pending row per loan application, so an older payload can't arrive after a newer one |
| Worker inside the API process | One deployable | Scales with the API. It can move to its own host (for example an Azure Function on a timer calling `OutboxDispatcher`), with `Outbox:Enabled=false` in the API |
| Delivered outbox rows are kept | Nothing to schedule | The table grows; a cleanup job should delete old rows |
| One loan application per customer, overwritten | The exercise asks for it | No history of past submissions |
| Denials aren't stored | Keeps the model small | No record of why someone was denied. In real US lending, denials need regulated notices (ECOA / Reg B) and record keeping |
| Advisory lock per SSN | No retry code, and no mixed updates | Holds a connection for the few ms between lookup and save |
| SSN hashed and encrypted at rest | Not in the brief, but a database leak alone shouldn't expose the most sensitive field. The hash can be indexed and searched; the encrypted copy can be decrypted for the partner | Two keys, a decrypt on every load, and no DbContext pooling: the SSN converter needs the encryption service injected (microseconds per request) |
| Migrations at API startup | No separate migration step; EF Core locks against concurrent runs | The app's database user needs schema rights |
| Browser calls the API directly | No backend-for-frontend to build | The API is public; CORS only restricts browsers |
