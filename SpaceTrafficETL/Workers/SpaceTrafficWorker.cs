using Microsoft.Extensions.Options;
using SpaceTrafficETL.Configuration;
using SpaceTrafficETL.Services;

namespace SpaceTrafficETL.Workers;

public sealed class SpaceTrafficWorker(
    IOptions<SpaceTrafficOptions> options,
    ISpaceTrafficEtlPipeline pipeline,
    ILogger<SpaceTrafficWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SpaceTraffic ETL worker started; interval is {Interval}", options.Value.Etl.Interval);

        if (!options.Value.Etl.RunOnStartup)
        {
            var firstRun = GetNextRun(DateTimeOffset.UtcNow, options.Value.Etl.DailyRunTimeUtc);
            logger.LogInformation("First SpaceTraffic ETL run scheduled for {FirstRunUtc}", firstRun);
            await Task.Delay(firstRun - DateTimeOffset.UtcNow, stoppingToken);
        }

        await RunSafelyAsync(stoppingToken);

        using var timer = new PeriodicTimer(options.Value.Etl.Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSafelyAsync(stoppingToken);
        }
    }

    private async Task RunSafelyAsync(CancellationToken cancellationToken)
    {
        var runStartedAt = DateTimeOffset.UtcNow;

        try
        {
            logger.LogInformation("SpaceTraffic ETL run started at {RunStartedAtUtc}", runStartedAt);
            await pipeline.RunOnceAsync(cancellationToken);
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
