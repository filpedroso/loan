# Loan application

A business loan application:
- a Next.js form
- a .NET 10 API that decides on each loan application
- PostgreSQL
- an outbox worker that sends approved loan applications to an external partner (mocked here)

How it's built and why: [ARCHITECTURE.md](ARCHITECTURE.md).

## Run

You need Docker (with Compose) and make. On Windows, run everything from WSL (Ubuntu).

```bash
make doctor   # checks your tools and prints how to install missing ones
make up       # builds and starts everything
```

| What | URL |
|---|---|
| Web app | http://localhost:3000 |
| API | http://localhost:8080 (health: `/health`) |
| External partner mock: what it received | http://localhost:8081/applications |
| Dashboard: logs, traces, metrics | http://localhost:18888 |

- `make down` stops everything and keeps the database. `docker compose -f infra/docker-compose.yml down -v` deletes the database too.
- `make fclean` leaves the folder as if just cloned. It stops and deletes the containers, the database, the images the stack built or pulled (the .NET and Node base images stay), and every git-ignored file: build output, `node_modules`, and also any `.env` or editor settings. Docker's build cache stays, because it can't be deleted per project; `docker builder prune` clears it for the whole machine.
- On WSL, keep a terminal open while you use the app; WSL stops the containers when its last session closes.

## Test

```bash
make test   # unit + integration tests; needs the .NET 10 SDK and Docker
make load   # k6 against the running stack: 10 s warm-up, then 100 requests/s for 30 s; must keep p95 < 200 ms and < 1% errors
```

- Integration tests start a throwaway PostgreSQL with Testcontainers.
- `make load` restarts the API tracing only 1 in 10 requests, so the dashboard's own work doesn't skew the result. The next `make up` traces every request again.
- k6 waits for `/health` and warms the API up before measuring. Only the measured 30 s count toward the thresholds.
- Frontend checks: `cd web && npm ci && npm run lint && npm run typecheck`.

## Sample data

None of these SSNs can belong to a real person: the SSA reserves 987-65-4320 to 987-65-4329 for advertising, and never issues SSNs starting with 666. The blocklist is seeded with `987-65-4328` and `987-65-4329` (`Blocklist:Seed` in `appsettings.json`).

| Scenario | SSN | State | Result |
|---|---|---|---|
| New customer | 987-65-4320 | CA | Approved |
| Same SSN, new amount or address | 987-65-4320 | TX | Approved; overwrites the customer and loan application, same reference |
| State not served | 987-65-4321 | NY | Denied: `StateNotServed` |
| Blocklisted | 987-65-4329 | FL | Denied: `NotEligible` |
| Both | 987-65-4328 | NY | Denied: `StateNotServed` (the SSN isn't checked) |

Without the web app:

```bash
curl -X POST http://localhost:8080/api/v1/applications -H 'Content-Type: application/json' -d '{
  "firstName": "Jane", "lastName": "Doe", "address": "1 Main St, Springfield, IL 62701",
  "state": "CA", "companyName": "Acme Corp", "ssn": "987-65-4320", "requestedAmount": 50000 }'
```

Within a second of an approval, the loan application shows up at http://localhost:8081/applications.

To simulate an external partner outage:
1. `docker compose -f infra/docker-compose.yml stop external-partner`
2. Submit a loan application. Warnings appear in the dashboard's structured logs.
3. `docker compose -f infra/docker-compose.yml start external-partner`. If it's back within about 10 seconds, the next tick delivers the message. Otherwise the message is dead-lettered after 10 failed attempts; the replay query in [ARCHITECTURE.md](ARCHITECTURE.md#outbox-worker) sends it again.

## Develop without containers

| Part | How to run it |
|---|---|
| Database | `docker compose -f infra/docker-compose.yml up -d postgres` |
| API | `dotnet run --project backend/src/Loans.Api`, on http://localhost:8080, using `appsettings.Development.json` |
| External partner mock | `dotnet run --project partner/ExternalPartner.Mock`, on http://localhost:8081 |
| Web | `cd web && npm ci && npm run dev`, on http://localhost:3000 |

Run `dotnet` commands for the backend (tests, `dotnet ef`) from `backend/`, where its `global.json`, tool manifest and solution are.

Next.js (Turbopack) can't build from a folder whose path contains `#`. If yours does, move the repository.
