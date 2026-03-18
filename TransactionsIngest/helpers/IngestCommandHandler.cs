using TransactionsIngest.Commands.ReconcileTransactions;

namespace TransactionsIngest.Helpers;

public sealed class IngestCommand : ICommand
{
    public string Name => "ingest";
    public string Description => "Run a single ingest cycle.";
}

public sealed class IngestCommandHandler : ICommandHandler<IngestCommand>
{
    private readonly ICommandHandler<ReconcileCommand> _reconcileHandler;

    public IngestCommandHandler(ICommandHandler<ReconcileCommand> reconcileHandler)
    {
        _reconcileHandler = reconcileHandler;
    }

    public async Task HandleAsync(IngestCommand command, CancellationToken cancellationToken = default)
    {
        await _reconcileHandler.HandleAsync(new ReconcileCommand(), cancellationToken);
        Console.WriteLine("Ingest run completed.");
    }
}

