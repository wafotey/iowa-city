using TransactionsApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

var aspNetCoreUrls = builder.Configuration["ASPNETCORE_URLS"] ?? string.Empty;
if (aspNetCoreUrls.Contains("https://", StringComparison.OrdinalIgnoreCase))
    app.UseHttpsRedirection();
var locations = new[] { "STO-01", "STO-02", "STO-03", "STO-04", "WEB-01" };
var products = new[]
{
    "Wireless Mouse",
    "USB-C Cable",
    "Keyboard",
    "Laptop Stand",
    "Webcam",
    "Phone Charger",
    "Monitor Lamp",
    "Desk Mat"
};

var sampleTransactions = SeedTransactions(locations, products);

app.MapGet("/transactions", () =>
    {
        MutateSomeTransactions(sampleTransactions, locations, products);
        return sampleTransactions;
    })
    .WithName("GetTransactions")
    .WithSummary("Returns a transaction snapshot for testing (timestamps within the last 48 hours).");

app.MapGet("/", () => Results.Redirect("/openapi/v1.json"))
    .ExcludeFromDescription();

await app.RunAsync();

static List<TransactionDto> SeedTransactions(string[] locations, string[] products)
{
    int count = Random.Shared.Next(5, 11);
    var list = new List<TransactionDto>(capacity: count);
    for (var i = 0; i < count; i++)
    {
        list.Add(new TransactionDto
        {
            TransactionId = 1001 + i, 
            CardNumber = RandomCardNumber(),
            LocationCode = locations[Random.Shared.Next(locations.Length)],
            ProductName = products[Random.Shared.Next(products.Length)],
            Amount = Math.Round((decimal)(Random.Shared.NextDouble() * 495 + 5), 2),
            Timestamp = RandomUtcTimestampInLast48Hours()
        });
    }

    return list;
}

static void MutateSomeTransactions(List<TransactionDto> transactions, string[] locations, string[] products)
{
    if (transactions.Count == 0)
        return;

    // Mutate a small subset on each request so ingest can detect updates/reconciliations.
    var changes = Math.Max(1, transactions.Count / 10);
    for (var i = 0; i < changes; i++)
    {
        var tx = transactions[Random.Shared.Next(transactions.Count)];
        var mutationType = Random.Shared.Next(4);

        switch (mutationType)
        {
            case 0:
                tx.Amount = Math.Round((decimal)(Random.Shared.NextDouble() * 495 + 5), 2);
                break;
            case 1:
                tx.ProductName = products[Random.Shared.Next(products.Length)];
                break;
            case 2:
                tx.LocationCode = locations[Random.Shared.Next(locations.Length)];
                break;
            default:
                tx.Timestamp = RandomUtcTimestampInLast48Hours();
                break;
        }
    }
}

static string RandomCardNumber()
{
    var prefixes = new[] { "4", "5" };
    var prefix = prefixes[Random.Shared.Next(prefixes.Length)];
    var digits = new System.Text.StringBuilder(prefix, 16);
    while (digits.Length < 16)
    {
        digits.Append(Random.Shared.Next(10));
    }

    return digits.ToString();
}

static DateTime RandomUtcTimestampInLast48Hours()
{
    var minutesBack = Random.Shared.Next(0, 48 * 60 + 1);
    return DateTime.UtcNow.AddMinutes(-minutesBack);
}
