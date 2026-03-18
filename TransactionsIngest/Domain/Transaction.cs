namespace TransactionsIngest.Data;

public enum TransactionStatus
{
    Active = 0,
    Revoked = 1,
    Finalized = 2
}

public sealed class Transaction
{
    public int Id { get; set; }
    public int TransactionId { get; set; }
    public string CardLast4 { get; set; } = string.Empty;
    public string LocationCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionTimeUtc { get; set; }
    public TransactionStatus Status { get; set; }
    public int Revision { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<TransactionAudit> AuditEntries { get; set; } = new List<TransactionAudit>();
}
