# Iowa City – Transactions Ingest & Test API

Two .NET 10 projects:

1. **TransactionsIngest** – Console app that ingests a last-24h transaction snapshot (per Specification.pdf): upsert by `TransactionId`, record changes, revoke missing transactions, optional finalization, idempotent runs with SQLite + EF Core.
2. **TransactionsApi** – Minimal Web API that exposes `GET /transactions` returning sample JSON for testing the ingest app.

## Build & run

```bash
# Restore (requires network)
dotnet restore

# Build
dotnet build

# Run ingest (uses mock file by default)
cd TransactionsIngest && dotnet run

# Run test API (then point ingest at it by setting UseMockFeed: false and ApiUrl)
cd TransactionsApi && dotnet run
```

## Configuration (TransactionsIngest)

Edit `TransactionsIngest/appsettings.json`:

- **UseMockFeed**: `true` = read from `mock-transactions.json`; `false` = HTTP GET from **ApiUrl**.
- **MockFeedPath**: path to JSON file (default `mock-transactions.json`).
- **ApiUrl**: URL for snapshot (e.g. `http://localhost:5000/transactions` when using TransactionsApi).
- **ConnectionString**: SQLite DB (default `Data Source=ingest.db`).

## Testing ingest with the API

1. Start TransactionsApi: `cd TransactionsApi && dotnet run`
2. In `TransactionsIngest/appsettings.json` set `"UseMockFeed": false` and `"ApiUrl": "http://localhost:PORT/transactions"` (use the port shown by the API).
3. Run ingest: `cd TransactionsIngest && dotnet run`

## Automated tests

```bash
dotnet test
```

Tests cover: insert of new transactions, update detection and audit, revocation when missing from snapshot, idempotency (repeated run with same input).

## Approach (TransactionsIngest)

- Single run per execution; each run is wrapped in one DB transaction for idempotency.
- Snapshot is fetched from mock file or HTTP; all records are upserted by `TransactionId`. Changes are detected and written to `TransactionAudits` (change type + changed fields).
- Any existing transaction in the 24h window that is not in the current snapshot is marked **Revoked** and audited.
- Records older than 24 hours can be marked **Finalized** and are not updated afterward.
- Card number is stored as last-4 only (`CardLast4`).

## Assumptions

- API returns UTC timestamps; non-UTC are converted to UTC.
- String fields are truncated to 20 chars (except `CardLast4`, 4).
- 24-hour window is based on `DateTime.UtcNow.AddHours(-24)`.
