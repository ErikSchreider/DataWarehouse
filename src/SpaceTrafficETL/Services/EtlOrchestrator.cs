using SpaceTrafficETL.Configuration;
using SpaceTrafficETL.Export;
using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Services;

public sealed class EtlOrchestrator(
    IDownloadService downloadService,
    ITleParserService tleParserService,
    IUcsParserService ucsParserService,
    ICsvExportService csvExportService,
    IDatabaseMigrationService migrationService,
    IExasolImportService importService,
    ILogger<EtlOrchestrator> logger)
    : IEtlOrchestrator
{
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var runTimestamp = DateTimeOffset.UtcNow;

        logger.LogInformation("Ensuring Exasol schema and tables exist");
        await migrationService.EnsureDatabaseAsync(cancellationToken);

        logger.LogInformation("Download started");
        var rawDatasets = await DownloadAllDatasetsAsync(cancellationToken);
        logger.LogInformation("Download completed with {DatasetCount} raw datasets", rawDatasets.Count);

        logger.LogInformation("Parsing TLE files");
        var celesTrakObjects = await ParseTleDatasetsAsync(rawDatasets, cancellationToken);
        var ucsSatellites = await ParseUcsDatasetsAsync(rawDatasets, cancellationToken);
        logger.LogInformation(
            "Parsing result: {ObjectCount} TLE objects and {UcsSatelliteCount} UCS satellites",
            celesTrakObjects.Count,
            ucsSatellites.Count);

        logger.LogInformation("Creating staging CSV exports");
        var celesTrakCsvPath = celesTrakObjects.Count > 0
            ? await csvExportService.ExportCelesTrakObjectsAsync(celesTrakObjects, runTimestamp, cancellationToken)
            : null;

        var ucsCsvPath = await csvExportService.ExportUcsSatellitesAsync(ucsSatellites, runTimestamp, cancellationToken);

        logger.LogInformation("Loading staging CSV files into Exasol");
        var importResults = await importService.ReloadStagingTablesAsync(celesTrakCsvPath, ucsCsvPath, cancellationToken);
        foreach (var result in importResults)
        {
            logger.LogInformation("Loaded {RowCount} rows into {TableName}", result.RowCount, result.TableName);
        }

        await migrationService.LoadWarehouseAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<RawDataset>> DownloadAllDatasetsAsync(CancellationToken cancellationToken)
    {
        var datasets = new List<RawDataset>();

        datasets.AddRange(await downloadService.DownloadCelesTrakAsync(cancellationToken));

        var ucsDataset = await downloadService.DownloadUcsAsync(cancellationToken);
        if (ucsDataset is not null)
        {
            datasets.Add(ucsDataset);
        }

        var launchDataset = await downloadService.DownloadLaunchDataAsync(cancellationToken);
        if (launchDataset is not null)
        {
            datasets.Add(launchDataset);
        }

        return datasets;
    }

    private async Task<IReadOnlyList<TleObject>> ParseTleDatasetsAsync(
        IEnumerable<RawDataset> rawDatasets,
        CancellationToken cancellationToken)
    {
        var objects = new List<TleObject>();

        foreach (var rawDataset in rawDatasets.Where(dataset => dataset.Kind == DataSourceKind.Tle))
        {
            var parsed = await tleParserService.ParseFileAsync(rawDataset.RawFilePath, cancellationToken);
            objects.AddRange(parsed);
            logger.LogInformation(
                "Parsed {ObjectCount} TLE objects from {SourceName}",
                parsed.Count,
                rawDataset.SourceName);
        }

        return objects;
    }

    private async Task<IReadOnlyList<UcsSatellite>> ParseUcsDatasetsAsync(
        IEnumerable<RawDataset> rawDatasets,
        CancellationToken cancellationToken)
    {
        var satellites = new List<UcsSatellite>();

        foreach (var rawDataset in rawDatasets.Where(dataset => dataset.Kind == DataSourceKind.UcsSatelliteDatabase))
        {
            var parsed = await ucsParserService.ParseFileAsync(rawDataset, cancellationToken);
            satellites.AddRange(parsed);
            logger.LogInformation(
                "Parsed {SatelliteCount} UCS satellites from {SourceName}",
                parsed.Count,
                rawDataset.SourceName);
        }

        return satellites;
    }
}
