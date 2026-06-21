using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Options;
using SpaceTrafficETL.Configuration;
using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Export;

public sealed class CsvExportService(IOptions<SpaceTrafficOptions> options, ILogger<CsvExportService> logger)
    : ICsvExportService
{
    private const string CelesTrakFileName = "stg_celestrak_objects.csv";
    private const string UcsFileName = "stg_ucs_satellites.csv";
    private const string LaunchFileName = "stg_launch_events.csv";

    public async Task<string> ExportCelesTrakObjectsAsync(
        IReadOnlyCollection<CelesTrakObject> objects,
        DateTimeOffset runTimestamp,
        CancellationToken cancellationToken)
    {
        var path = GetOutputPath(runTimestamp, CelesTrakFileName);
        await WriteRecordsAsync(
            path,
            objects.OrderBy(record => record.NoradId).ThenBy(record => record.Epoch).ThenBy(record => record.SourceGroup),
            new CelesTrakObjectMap(),
            cancellationToken);

        logger.LogInformation("Exported {RecordCount} CelesTrak objects to {CsvPath}", objects.Count, path);
        return path;
    }

    public async Task<string> ExportLaunchesAsync(
        IReadOnlyCollection<LaunchRecord> launches,
        DateTimeOffset runTimestamp,
        CancellationToken cancellationToken)
    {
        var path = GetOutputPath(runTimestamp, LaunchFileName);
        await WriteRecordsAsync(
            path,
            launches.OrderBy(record => record.LaunchDate).ThenBy(record => record.LaunchName),
            new LaunchRecordMap(),
            cancellationToken);

        logger.LogInformation("Exported {RecordCount} launch events to {CsvPath}", launches.Count, path);
        return path;
    }

    public async Task<string> ExportUcsSatellitesAsync(
        IReadOnlyCollection<UcsSatellite> satellites,
        DateTimeOffset runTimestamp,
        CancellationToken cancellationToken)
    {
        var path = GetOutputPath(runTimestamp, UcsFileName);
        await WriteRecordsAsync(path, satellites.OrderBy(record => record.NoradId).ThenBy(record => record.Name), new UcsSatelliteMap(), cancellationToken);

        logger.LogInformation("Exported {RecordCount} UCS satellites to {CsvPath}", satellites.Count, path);
        return path;
    }

    private async Task WriteRecordsAsync<TRecord, TMap>(
        string path,
        IEnumerable<TRecord> records,
        TMap classMap,
        CancellationToken cancellationToken)
        where TMap : ClassMap<TRecord>
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

        await using var stream = File.Create(path);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await using var csv = new CsvWriter(writer, CreateCsvConfiguration());

        csv.Context.RegisterClassMap(classMap);

        if (options.Value.Csv.IncludeHeader)
        {
            csv.WriteHeader<TRecord>();
            await csv.NextRecordAsync();
        }

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            csv.WriteRecord(record);
            await csv.NextRecordAsync();
        }
    }

    private CsvConfiguration CreateCsvConfiguration()
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = options.Value.Csv.Delimiter,
            HasHeaderRecord = options.Value.Csv.IncludeHeader,
            NewLine = Environment.NewLine
        };
    }

    private string GetOutputPath(DateTimeOffset runTimestamp, string fileName)
    {
        var outputDirectory = Path.Combine(
            options.Value.DataDirectories.StorageRoot,
            options.Value.Csv.OutputDirectory,
            runTimestamp.ToString("yyyy-MM-dd"));

        return Path.Combine(outputDirectory, fileName);
    }

    private sealed class CelesTrakObjectMap : ClassMap<CelesTrakObject>
    {
        public CelesTrakObjectMap()
        {
            Map(record => record.ObjectName).Name("object_name");
            Map(record => record.NoradId).Name("norad_id");
            Map(record => record.Epoch).Name("epoch").TypeConverterOption.Format("yyyy-MM-dd HH:mm:ss.ffffff");
            Map(record => record.InclinationDegrees).Name("inclination_deg");
            Map(record => record.Eccentricity).Name("eccentricity");
            Map(record => record.MeanMotionRevolutionsPerDay).Name("mean_motion");
            Map(record => record.SourceGroup).Name("source_group");
            Map(record => record.DownloadedAt).Name("imported_at").TypeConverterOption.Format("yyyy-MM-dd HH:mm:ss.ffffff");
        }
    }

    private sealed class UcsSatelliteMap : ClassMap<UcsSatellite>
    {
        public UcsSatelliteMap()
        {
            Map(record => record.NoradId).Name("norad_id");
            Map(record => record.Name).Name("satellite_name");
            Map(record => record.Country).Name("country");
            Map(record => record.Operator).Name("operator_name");
            Map(record => record.Purpose).Name("purpose");
            Map(record => record.OrbitClass).Name("orbit_type");
            Map(record => record.OperationalStatus).Name("operational_status");
            Map(record => record.LaunchDate).Name("launch_date").TypeConverterOption.Format("yyyy-MM-dd");
            Map(record => record.DownloadedAt).Name("imported_at").TypeConverterOption.Format("yyyy-MM-dd HH:mm:ss.ffffff");
        }
    }

    private sealed class LaunchRecordMap : ClassMap<LaunchRecord>
    {
        public LaunchRecordMap()
        {
            Map(record => record.LaunchId).Name("launch_id");
            Map(record => record.LaunchName).Name("launch_name");
            Map(record => record.LaunchDate).Name("launch_date").TypeConverterOption.Format("yyyy-MM-dd HH:mm:ss.ffffff");
            Map(record => record.LaunchProvider).Name("launch_provider");
            Map(record => record.RocketName).Name("rocket_name");
            Map(record => record.LaunchCountry).Name("launch_country");
            Map(record => record.LaunchStatus).Name("launch_status");
            Map(record => record.PayloadCount).Name("payload_count");
            Map(record => record.DownloadedAt).Name("imported_at").TypeConverterOption.Format("yyyy-MM-dd HH:mm:ss.ffffff");
        }
    }
}
