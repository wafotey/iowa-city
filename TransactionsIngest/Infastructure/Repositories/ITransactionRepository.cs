using TransactionsIngest.Data;

namespace TransactionsIngest.Repositories;

public interface ITransactionRepository
{

    void AddTransaction(Transaction transaction);
    bool TransactionExists(int transactionId, out Transaction? transaction);
    Task<Dictionary<int, Transaction>> GetCurrentSnapshotWithin24hrsAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default);

    Task<List<Transaction>> GetTransactionsEligibleForFinalizationAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default);
}