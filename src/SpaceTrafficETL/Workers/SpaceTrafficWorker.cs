using Microsoft.Extensions.Options;
using SpaceTrafficETL.Configuration;
using SpaceTrafficETL.Services;

namespace SpaceTrafficETL.Workers;

public sealed class SpaceTrafficWorker(
    IOptions<SpaceTrafficOptions> options,
    IEtlOrchestrator orchestrator,
    ILogger<SpaceTrafficWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "SpaceTraffic ETL worker started; daily run time is {DailyRunTimeUtc} UTC; run on startup is {RunOnStartup}",
            options.Value.Etl.DailyRunTimeUtc,
            options.Value.Etl.RunOnStartup);

        if (options.Value.Etl.RunOnStartup)
        {
            await RunSafelyAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = GetNextRun(DateTimeOffset.UtcNow, options.Value.Etl.DailyRunTimeUtc);
            logger.LogInformation("Next SpaceTraffic ETL run scheduled for {NextRunUtc}", nextRun);
            await Task.Delay(nextRun - DateTimeOffset.UtcNow, stoppingToken);
            await RunSafelyAsync(stoppingToken);
        }
    }

    private async Task RunSafelyAsync(CancellationToken cancellationToken)
    {
        var runStartedAt = DateTimeOffset.UtcNow;

        try
        {
            logger.LogInformation("SpaceTraffic ETL run started at {RunStartedAtUtc}", runStartedAt);
            await orchestrator.RunOnceAsync(cancellationToken);
            logger.LogInformation(
                "SpaceTraffic ETL run completed in {ElapsedMilliseconds} ms",
                (DateTimeOffset.UtcNow - runStartedAt).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("SpaceTraffic ETL worker is stopping");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled SpaceTraffic ETL run failure");
        }
    }

    private static DateTimeOffset GetNextRun(DateTimeOffset nowUtc, TimeSpan dailyRunTimeUtc)
    {
        var today = new DateTimeOffset(nowUtc.Date, TimeSpan.Zero).Add(dailyRunTimeUtc);
        return today > nowUtc ? today : today.AddDays(1);
    }
}
