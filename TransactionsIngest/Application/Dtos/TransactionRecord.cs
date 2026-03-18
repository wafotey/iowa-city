namespace TransactionsIngest.Models;

public sealed class TransactionRecord
{
    public int TransactionId { get; init; }
    public string CardNumber { get; init; } = string.Empty;
    public string LocationCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime Timestamp { get; init; }
}
