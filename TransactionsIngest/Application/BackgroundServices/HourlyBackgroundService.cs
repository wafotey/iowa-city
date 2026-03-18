using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TransactionsIngest.Helpers;

namespace TransactionsIngest.Services;

public sealed class HourlyBackgroundService : BackgroundService
{
    private static readonly TimeSpan ExecutionInterval = TimeSpan.FromMinutes(1);// TimeSpan.FromHours(1);
    private const string ReconcileCommandName = "ReconcileCommand";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HourlyBackgroundService> _logger;

    public HourlyBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<HourlyBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HourlyBackgroundService started.");

        // Run once at startup, then continue every hour.
        await DispatchReconcileCommandAsync(stoppingToken);

        using var timer = new PeriodicTimer(ExecutionInterval);

        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            await DispatchReconcileCommandAsync(stoppingToken);
        }
    }

    private async Task DispatchReconcileCommandAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<CommandDispatcher>();

            await dispatcher.ExecuteAsync(ReconcileCommandName, cancellationToken);

            _logger.LogInformation(
                "Dispatched '{CommandName}' at {TimestampUtc}.",
                ReconcileCommandName,
                DateTime.UtcNow);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("HourlyBackgroundService stopping.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch '{CommandName}'.", ReconcileCommandName);
        }
    }
}

  