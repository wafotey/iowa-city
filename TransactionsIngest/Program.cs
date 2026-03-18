using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TransactionsIngest;
using TransactionsIngest.Commands.ReconcileTransactions;
using TransactionsIngest.Data;
using TransactionsIngest.Data.Interceptors;
using TransactionsIngest.Helpers;
using TransactionsIngest.Repositories;

var builder = Host.CreateApplicationBuilder(args);
var ingestSection = builder.Configuration.GetSection(IngestOptions.SectionName);
var configuredConnectionString = ingestSection["ConnectionString"] ?? "Data Source=ingest.db";
var projectDirectory = FindProjectDirectory();
var databaseDirectory = Path.Combine(projectDirectory, "Infastructure", "database");
var connectionString = NormalizeSqliteConnectionString(configuredConnectionString, databaseDirectory);

builder.Services.Configure<IngestOptions>(ingestSection);
builder.Services.AddScoped<TransactionAuditSaveChangesInterceptor>();
builder.Services.AddDbContext<IngestDbContext>((sp, o) =>
{
    o.UseSqlite(connectionString);
    o.AddInterceptors(sp.GetRequiredService<TransactionAuditSaveChangesInterceptor>());
});

builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddSingleton<ICommand, IngestCommand>();
builder.Services.AddSingleton<ICommand, ReconcileCommand>();
builder.Services.AddScoped<ICommandHandler<IngestCommand>, IngestCommandHandler>();
builder.Services.AddScoped<ICommandHandler<ReconcileCommand>, ReconcileCommandHandler>();
builder.Services.AddSingleton<CommandDispatcher>();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<IngestDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    var dispatcher = scope.ServiceProvider.GetRequiredService<CommandDispatcher>();
    await dispatcher.RouteAsync(args);
}

await host.StopAsync();

static string FindProjectDirectory()
{
    var candidates = new[]
    {
        Directory.GetCurrentDirectory(),
        AppContext.BaseDirectory
    };

    foreach (var candidate in candidates)
    {
        var directory = new DirectoryInfo(candidate);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TransactionsIngest.csproj")))
                return directory.FullName;

            directory = directory.Parent;
        }
    }

    // Fallback to current directory if project file discovery fails.
    return Directory.GetCurrentDirectory();
}

static string NormalizeSqliteConnectionString(string rawConnectionString, string databaseDirectory)
{
    Directory.CreateDirectory(databaseDirectory);

    var connectionBuilder = new SqliteConnectionStringBuilder(rawConnectionString);
    var dataSource = connectionBuilder.DataSource;

    if (string.IsNullOrWhiteSpace(dataSource))
    {
        connectionBuilder.DataSource = Path.Combine(databaseDirectory, "ingest.db");
        return connectionBuilder.ToString();
    }

    if (!Path.IsPathRooted(dataSource))
        connectionBuilder.DataSource = Path.Combine(databaseDirectory, dataSource);

    var dbPath = connectionBuilder.DataSource;
    var parentDirectory = Path.GetDirectoryName(dbPath);
    if (!string.IsNullOrWhiteSpace(parentDirectory))
        Directory.CreateDirectory(parentDirectory);

    return connectionBuilder.ToString();
}
