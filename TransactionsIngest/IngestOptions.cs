namespace TransactionsIngest;

public sealed class IngestOptions
{
    public const string SectionName = "Ingest";

    public string? ApiUrl { get; set; }
    public string? ConnectionString { get; set; }
}
