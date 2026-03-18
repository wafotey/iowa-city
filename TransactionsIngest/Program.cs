using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TransactionsIngest;
using TransactionsIngest.Application.Decorators;
using TransactionsIngest.Application.Helpers;
using TransactionsIngest.Commands.ReconcileTransactions;
using TransactionsIngest.Data;
using TransactionsIngest.Data.Interceptors;
using TransactionsIngest.Helpers;
using TransactionsIngest.Repositories;
using TransactionsIngest.Services;

var hostBuilder = Host.CreateDefaultBuilder(args)
    .UseServiceProviderFactory(new AutofacServiceProviderFactory())
    .ConfigureServices((context, services) =>
    {
        var ingestSection = context.Configuration.GetSection(IngestOptions.SectionName);
        var configuredConnectionString = ingestSection["ConnectionString"] ?? "Data Source=ingest.db";
        var projectDirectory = FindProjectDirectory();
        var databaseDirectory = Path.Combine(projectDirectory, "Infastructure", "database");
        var connectionString = NormalizeSqliteConnectionString(configuredConnectionString, databaseDirectory);

        services.Configure<IngestOptions>(ingestSection);
        services.AddScoped<TransactionAuditSaveChangesInterceptor>();
        services.AddDbContext<IngestDbContext>((sp, o) =>
        {
            o.UseSqlite(connectionString);
            o.AddInterceptors(sp.GetRequiredService<TransactionAuditSaveChangesInterceptor>());
        });

        services.AddHttpClient<ITransactionFeed, HttpTransactionFeed>();
        services.AddHostedService<HourlyBackgroundService>();
    })
    .ConfigureContainer<ContainerBuilder>(container =>
    {
        container.RegisterType<TransactionRepository>()
            .As<ITransactionRepository>()
            .InstancePerLifetimeScope();

        container.RegisterType<IngestService>()
            .InstancePerLifetimeScope();

        container.RegisterType<ReconcileCommand>()
            .As<ICommand>()
            .SingleInstance();

        container.RegisterType<ReconcileCommandHandler>()
            .As<ICommandHandler<ReconcileCommand>>()
            .InstancePerLifetimeScope();

        container.RegisterGenericDecorator(
            typeof(DatabaseTransactionDecorator<>),
            typeof(ICommandHandler<>));

        container.RegisterType<CommandDispatcher>()
            .SingleInstance();
    });

var host = hostBuilder.Build();

using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<IngestDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

if (args.Length > 0)
{
    using var scope = host.Services.CreateScope();
    var dispatcher = scope.ServiceProvider.GetRequiredService<CommandDispatcher>();
    await dispatcher.ExecuteAsync(args[0]);
    await host.StopAsync();
    return;
}

await host.RunAsync();

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
