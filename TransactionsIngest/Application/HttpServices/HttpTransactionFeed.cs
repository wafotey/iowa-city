using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using TransactionsIngest.Models;

namespace TransactionsIngest.Services;

public sealed class HttpTransactionFeed : ITransactionFeed
{
    private readonly HttpClient _httpClient;
    private readonly IngestOptions _options;

    public HttpTransactionFeed(HttpClient httpClient, IOptions<IngestOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<Dictionary<int, TransactionRecord>> GetLast24HoursSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiUrl))
            throw new InvalidOperationException("Ingest:ApiUrl is not configured.");

        var transactions = await _httpClient.GetFromJsonAsync<List<TransactionRecord>>(
            _options.ApiUrl,
            cancellationToken);

        return (transactions ?? new List<TransactionRecord>())
            .ToDictionary(t => t.TransactionId, t => t);
    }
}
