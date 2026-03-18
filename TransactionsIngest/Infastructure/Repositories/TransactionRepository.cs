using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Data;

namespace TransactionsIngest.Repositories;

public sealed class TransactionRepository : ITransactionRepository
{
    private readonly IngestDbContext _dbContext;

    public TransactionRepository(IngestDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void AddTransaction(Transaction transaction) => _dbContext.Transactions.Add(transaction);

    public bool TransactionExists(int transactionId, out Transaction? transaction)
    {
        transaction = _dbContext.Transactions.FirstOrDefault(t => t.TransactionId == transactionId);
        return transaction != null;
    }

    public Task<Dictionary<int, Transaction>> GetCurrentSnapshotWithin24hrsAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default)
        => _dbContext.Transactions
            .Where(t => t.TransactionTimeUtc >= cutoffUtc)
            .ToDictionaryAsync(t => t.TransactionId, t => t, cancellationToken);

    public Task<List<Transaction>> GetTransactionsEligibleForFinalizationAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default)
        => _dbContext.Transactions
            .Where(t => t.TransactionTimeUtc < cutoffUtc && t.Status != TransactionStatus.Finalized)
            .ToListAsync(cancellationToken);
}