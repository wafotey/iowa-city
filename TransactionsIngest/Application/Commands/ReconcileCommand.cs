using TransactionsIngest.Application.Helpers;
using TransactionsIngest.Services;

namespace TransactionsIngest.Commands.ReconcileTransactions;

public sealed class ReconcileCommand : ICommand{}

public sealed class ReconcileCommandHandler : ICommandHandler<ReconcileCommand>
{
    private readonly IngestService _ingestService;

    public ReconcileCommandHandler(IngestService ingestService)
    {
        _ingestService = ingestService;
    }

    public async Task HandleAsync(ReconcileCommand command, CancellationToken cancellationToken = default)
    {
        await _ingestService.RunAsync(cancellationToken);
    }
}