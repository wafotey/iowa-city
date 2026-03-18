namespace TransactionsIngest.Data;

public sealed class TransactionAudit
{
    public int Id { get; set; }
    public int TransactionId { get; set; }
    public int Revision { get; set; }
    public string ChangeType { get; set; } = string.Empty; // "Insert", "Update", "Revoke", "Finalize"
    public string? ChangedFields { get; set; }
    public string? BeforeChanges { get; set; }
    public string? AfterChanges { get; set; }
    public DateTime RecordedAtUtc { get; set; }

    public int TransactionEntityId { get; set; }
    public Transaction Transaction { get; set; } = null!;
}
