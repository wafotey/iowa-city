using TransactionsIngest.Data;
using TransactionsIngest.Helpers;
using TransactionsIngest.Repositories;

namespace TransactionsIngest.Commands.ReconcileTransactions;

public sealed class ReconcileCommand : ICommand
{
    public string Name => "reconcile";
    public string Description => "Reconcile transactions and finalize records older than 24 hours.";
}

public sealed class ReconcileCommandHandler : ICommandHandler<ReconcileCommand>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IngestDbContext _dbContext;

    public ReconcileCommandHandler(ITransactionRepository transactionRepository, IngestDbContext dbContext)
    {
        _transactionRepository = transactionRepository;
        _dbContext = dbContext;
    }

    public async Task HandleAsync(ReconcileCommand command, CancellationToken cancellationToken = default)
    {
        var cutoffUtc = DateTime.UtcNow.AddHours(-24);
        var toFinalize = await _transactionRepository.GetTransactionsEligibleForFinalizationAsync(cutoffUtc, cancellationToken);

        foreach (var transaction in toFinalize)
            transaction.Status = TransactionStatus.Finalized;

        await _dbContext.SaveChangesAsync(cancellationToken);

        Console.WriteLine($"Reconcile completed. Finalized {toFinalize.Count} transaction(s).");
    }
}