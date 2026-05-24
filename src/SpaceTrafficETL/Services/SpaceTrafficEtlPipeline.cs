using SpaceTrafficETL.Configuration;
using SpaceTrafficETL.Export;
using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Services;

public sealed class SpaceTrafficEtlPipeline(
    IDownloadService downloadService,
    ITleParserService tleParserService,
    ICsvExportService csvExportService,
    ILogger<SpaceTrafficEtlPipeline> logger)
    : ISpaceTrafficEtlPipeline
{
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var runTimestamp = DateTimeOffset.UtcNow;
        var celesTrakObjects = new List<TleObject>();
        var ucsSatellites = new List<UcsSatellite>();

        logger.LogInformation("ETL stage 1/3: downloading all configured datasets");
        var rawDatasets = await DownloadRawDatasetsAsync(cancellationToken);

        logger.LogInformation("ETL stage 2/3: parsing downloaded datasets");

        foreach (var rawDataset in rawDatasets)
        {
            try
            {
                if (rawDataset.Kind == DataSourceKind.Tle)
                {
                    var tleObjects = await tleParserService.ParseFileAsync(rawDataset.RawFilePath, cancellationToken);
                    celesTrakObjects.AddRange(tleObjects);
                }
                else
                {
                    logger.LogInformation(
                        "Source {SourceName} was archived only. Parser/export mapping for {SourceKind} is intentionally not implemented yet.",
                        rawDataset.SourceName,
                        rawDataset.Kind);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Failed to process source {SourceName}", rawDataset.SourceName);
            }
        }

        logger.LogInformation(
            "Parsed {CelesTrakObjectCount} CelesTrak TLE objects and {UcsSatelliteCount} UCS satellite rows",
            celesTrakObjects.Count,
            ucsSatellites.Count);

        logger.LogInformation("ETL stage 3/3: creating staging CSV exports");

        if (celesTrakObjects.Count > 0)
        {
            var stagingPath = await csvExportService.ExportCelesTrakObjectsAsync(celesTrakObjects, runTimestamp, cancellationToken);
            logger.LogInformation("Created Exasol staging CSV with {RecordCount} CelesTrak records at {StagingPath}", celesTrakObjects.Count, stagingPath);
        }
        else
        {
            logger.LogWarning("No CelesTrak TLE objects were parsed; skipping CelesTrak staging export");
        }

        var ucsStagingPath = await csvExportService.ExportUcsSatellitesAsync(ucsSatellites, runTimestamp, cancellationToken);
        logger.LogInformation("Created UCS staging CSV with {RecordCount} records at {StagingPath}", ucsSatellites.Count, ucsStagingPath);
    }

    private async Task<IReadOnlyList<RawDataset>> DownloadRawDatasetsAsync(CancellationToken cancellationToken)
    {
        var datasets = new List<RawDataset>();

        try
        {
            datasets.AddRange(await downloadService.DownloadCelesTrakAsync(cancellationToken));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "CelesTrak download step failed");
        }

        try
        {
            var ucsDataset = await downloadService.DownloadUcsAsync(cancellationToken);
            if (ucsDataset is not null)
            {
                datasets.Add(ucsDataset);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "UCS download step failed");
        }

        try
        {
            var launchDataset = await downloadService.DownloadLaunchDataAsync(cancellationToken);
            if (launchDataset is not null)
            {
                datasets.Add(launchDataset);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Launch data download step failed");
        }

        logger.LogInformation("Downloaded {DatasetCount} raw datasets", datasets.Count);
        return datasets;
    }
}
