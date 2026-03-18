using TransactionsIngest.Models;

namespace TransactionsIngest.Services;

public interface ITransactionFeed
{
    Task<Dictionary<int, TransactionRecord>> GetLast24HoursSnapshotAsync(
        CancellationToken cancellationToken = default);
}
