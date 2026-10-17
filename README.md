# Loan application

Apply for a business loan and get a decision right away. Approved applications are saved and sent to an external partner.

- `web/`: a Next.js form
- `backend/`: a .NET 10 API that decides and saves, with an outbox worker that sends approvals to the partner
- `partner/`: a mock of the external partner
- `infra/`: Docker Compose (PostgreSQL, the services above, a dashboard) and the k6 load test

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
- `make fclean` leaves the folder as if just cloned: it deletes the containers, the database, the images the stack built or pulled, and every git-ignored file (build output, `node_modules`, and any `.env` or editor settings). The .NET and Node base images and Docker's build cache stay; `docker builder prune` clears the cache for the whole machine.
- On WSL, keep a terminal open while you use the app; WSL stops the containers when its last session closes.

## Test

```bash
make test   # backend unit + integration tests and frontend tests; needs the .NET 10 SDK, Node and Docker
make load   # k6 against the running stack: 10 s warm-up, then 100 requests/s for 30 s; must keep p95 < 200 ms and < 1% errors
```

- Integration tests start a throwaway PostgreSQL with Testcontainers.
- `make load` restarts the API tracing only 1 in 10 requests, so the dashboard's own work doesn't skew the result. The next `make up` traces every request again.
- k6 waits for `/health` and warms the API up before measuring. Only the measured 30 s count toward the thresholds.
- Frontend checks: `cd web && npm ci && npm run lint && npm run typecheck && npm test`.

## Sample data

None of these SSNs can belong to a real person. The blocklist is seeded with `987-65-4328` and `987-65-4329` (`Blocklist:Seed` in `appsettings.json`).

| Scenario | SSN | State | Result |
|---|---|---|---|
| New customer | 987-65-4320 | CA | Approved |
| Same SSN, new amount or address | 987-65-4320 | TX | Approved; overwrites the customer and loan application, same reference |
| State not served | 987-65-4321 | NY | Denied: `StateNotServed` |
| Blocklisted | 987-65-4329 | FL | Denied: `NotEligible` |
| Both | 987-65-4328 | NY | Denied: `StateNotServed` (the SSN isn't checked) |

Within a second of an approval, the loan application shows up at http://localhost:8081/applications.
