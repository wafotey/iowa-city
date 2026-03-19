using TransactionsIngest.Data;
using TransactionsIngest.Models;
using TransactionsIngest.Repositories;

namespace TransactionsIngest.Services;

public sealed class IngestService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ITransactionFeed _feed;
    private const int HoursWindow = 24;

    public IngestService(ITransactionRepository transactionRepository, ITransactionFeed feed)
    {
        _transactionRepository = transactionRepository;
        _feed = feed;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var cutoffUtc = DateTime.UtcNow.AddHours(-HoursWindow);
        var snapshot = await _feed.GetLast24HoursSnapshotAsync(cancellationToken);
        var existing = await GetCurrentSnapshotAsync(cutoffUtc, cancellationToken);

        DetectChangesInTransactions(snapshot, existing);
        RevokeMissingFromSnapshot(existing, snapshot);
        await FinalizeTransactionOlderThan24HoursAsync(cutoffUtc, cancellationToken);
    }

    public async Task<Dictionary<int, Transaction>>  GetCurrentSnapshotAsync( DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        return await _transactionRepository.GetCurrentSnapshotWithin24hrsAsync(cutoffUtc, cancellationToken);
    }
   

    public void DetectChangesInTransactions(IDictionary<int, TransactionRecord> snapshot, IDictionary<int, Transaction> existing)
    {
        foreach (var record in snapshot.Values)
        {
            if (!_transactionRepository.TransactionExists(record.TransactionId, out var existingEntity))
            {
                Insert(record);
                continue;
            }

            if (existingEntity?.Status != TransactionStatus.Finalized)
                Upsert(existingEntity!, record);
        }
    }

  

    public static void RevokeMissingFromSnapshot(Dictionary<int, Transaction> existing, Dictionary<int, TransactionRecord> snapshot)
    {
        foreach (var entity in existing.Where(entity => !snapshot.ContainsKey(entity.Key)))
        {
            Revoke(entity.Value);
        }
    }


    public async Task FinalizeTransactionOlderThan24HoursAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        var toFinalize = await _transactionRepository.GetTransactionsEligibleForFinalizationAsync(cutoffUtc, cancellationToken);
        foreach (var entity in toFinalize)
        {
            entity.Status = TransactionStatus.Finalized;
        }
    }

    private static string Last4(string cardNumber)
    {
        if (string.IsNullOrEmpty(cardNumber) || cardNumber.Length < 4)
            return cardNumber;
        return cardNumber.Length <= 4 ? cardNumber : cardNumber[^4..];
    }

    private static string TrimToMaxLength(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Length > maxLength ? value[..maxLength] : value;
    }

    private void Insert(TransactionRecord record)
    {
        var entity = new Transaction
        {
            TransactionId = record.TransactionId,
            CardLast4 = Last4(record.CardNumber),
            LocationCode = TrimToMaxLength(record.LocationCode, 20),
            ProductName = TrimToMaxLength(record.ProductName, 20),
            Amount = record.Amount,
            TransactionTimeUtc = record.Timestamp.Kind == DateTimeKind.Utc ? record.Timestamp : record.Timestamp.ToUniversalTime(),
            Status = TransactionStatus.Active
        };
        _transactionRepository.AddTransaction(entity);
    }

    private static void Upsert(Transaction entity, TransactionRecord record)
    {
        var cardLast4 = Last4(record.CardNumber);
        var locationCode = TrimToMaxLength(record.LocationCode, 20);
        var productName = TrimToMaxLength(record.ProductName, 20);
        var transactionTimeUtc = record.Timestamp.Kind == DateTimeKind.Utc ? record.Timestamp : record.Timestamp.ToUniversalTime();

        var hasChanges =
            entity.CardLast4 != cardLast4 ||
            entity.LocationCode != locationCode ||
            entity.ProductName != productName ||
            entity.Amount != record.Amount ||
            entity.TransactionTimeUtc != transactionTimeUtc ||
            entity.Status != TransactionStatus.Active;

        if (!hasChanges)
            return;

        
        entity.CardLast4 = cardLast4;
        entity.LocationCode = locationCode;
        entity.ProductName = productName;
        entity.Amount = record.Amount;
        entity.TransactionTimeUtc = transactionTimeUtc;
        entity.Status = TransactionStatus.Active;
    }

    private static void Revoke(Transaction entity)
    {
        entity.Status = TransactionStatus.Revoked;
    }
}
