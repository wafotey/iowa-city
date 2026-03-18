# Iowa City - Transactions Ingest and Transactions API

This solution contains two .NET 10 projects:

1. TransactionsApi
2. TransactionsIngest

For full assignment context and acceptance details, see Specification.pdf at the repository root.

## Run Order (Important)

1. Run TransactionsApi first.
2. Next run TransactionsIngest.

### Commands

```bash
dotnet restore
dotnet build

# terminal 1
cd TransactionsApi
dotnet run

# terminal 2
cd TransactionsIngest
dotnet run
```

## Database

The project uses SQLite.

The SQLite database is created on startup in:

- TransactionsIngest/Infastructure/database

## Architecture and Design Patterns

The project demonstrates the following patterns:

1. Command pattern
   - A ReconcileCommand is dispatched to start processing transactions.

2. Facade pattern
   - IngestService is a facade over the repository layer and provides a single abstraction for transaction processing workflows.

3. Decorator pattern
   - DatabaseTransactionDecorator persists command results by calling SaveChangesAsync in one centralized place.
   - This avoids injecting DbContext everywhere and keeps persistence concerns clean and consistent whenever a command is run.

4. Repository pattern
   - Repository abstractions sit on top of DbContext and isolate data access concerns from higher-level orchestration logic.

## Disclaimer

Although this approach is overkill for a small sample, it is meant for demonstration only.

## Effort

The project took close to 3 hours of effort.
