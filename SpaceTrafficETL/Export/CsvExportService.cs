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

    public async Task<string> ExportCelesTrakObjectsAsync(
        IReadOnlyCollection<TleObject> objects,
        DateTimeOffset runTimestamp,
        CancellationToken cancellationToken)
    {
        var path = GetOutputPath(runTimestamp, CelesTrakFileName);
        await WriteRecordsAsync(path, objects.OrderBy(record => record.NoradId), new CelesTrakObjectMap(), cancellationToken);

        logger.LogInformation("Exported {RecordCount} CelesTrak objects to {CsvPath}", objects.Count, path);
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

    private sealed class CelesTrakObjectMap : ClassMap<TleObject>
    {
        public CelesTrakObjectMap()
        {
            Map(record => record.NoradId).Name("NORAD_ID");
            Map(record => record.ObjectName).Name("OBJECT_NAME");
            Map(record => record.Classification).Name("CLASSIFICATION");
            Map(record => record.InternationalDesignator).Name("INTERNATIONAL_DESIGNATOR");
            Map(record => record.Epoch).Name("EPOCH_UTC").TypeConverterOption.Format("O");
            Map(record => record.EpochYear).Name("EPOCH_YEAR");
            Map(record => record.EpochDay).Name("EPOCH_DAY");
            Map(record => record.InclinationDegrees).Name("INCLINATION_DEGREES");
            Map(record => record.Eccentricity).Name("ECCENTRICITY");
            Map(record => record.MeanMotionRevolutionsPerDay).Name("MEAN_MOTION_REV_PER_DAY");
            Map(record => record.Line1).Name("TLE_LINE_1");
            Map(record => record.Line2).Name("TLE_LINE_2");
            Map(record => record.ParsedAt).Name("PARSED_AT_UTC").TypeConverterOption.Format("O");
        }
    }

    private sealed class UcsSatelliteMap : ClassMap<UcsSatellite>
    {
        public UcsSatelliteMap()
        {
            Map(record => record.NoradId).Name("NORAD_ID");
            Map(record => record.Name).Name("SATELLITE_NAME");
            Map(record => record.Operator).Name("OPERATOR");
            Map(record => record.Country).Name("COUNTRY");
            Map(record => record.Purpose).Name("PURPOSE");
            Map(record => record.OrbitClass).Name("ORBIT_CLASS");
            Map(record => record.LaunchDate).Name("LAUNCH_DATE").TypeConverterOption.Format("yyyy-MM-dd");
            Map(record => record.SourceName).Name("SOURCE_NAME");
            Map(record => record.DownloadedAt).Name("DOWNLOADED_AT_UTC").TypeConverterOption.Format("O");
            Map(record => record.RawFilePath).Name("RAW_FILE_PATH");
        }
    }
}
