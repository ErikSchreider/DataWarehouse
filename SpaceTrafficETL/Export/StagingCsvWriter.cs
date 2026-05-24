using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using SpaceTrafficETL.Configuration;
using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Export;

public sealed class StagingCsvWriter(IOptions<SpaceTrafficOptions> options, ILogger<StagingCsvWriter> logger)
    : IStagingCsvWriter
{
    public async Task<string> WriteSatelliteStageAsync(
        IReadOnlyCollection<StagingSatelliteRecord> records,
        DateTimeOffset runTimestamp,
        CancellationToken cancellationToken)
    {
        var stagingDirectory = Path.Combine(
            options.Value.DataDirectories.StorageRoot,
            options.Value.DataDirectories.Staging,
            runTimestamp.Year.ToString("0000"),
            runTimestamp.Month.ToString("00"),
            runTimestamp.Day.ToString("00"));

        Directory.CreateDirectory(stagingDirectory);

        var path = Path.Combine(stagingDirectory, "satellite_tle_stage.csv");
        await using var stream = File.Create(path);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (options.Value.Csv.IncludeHeader)
        {
            await writer.WriteLineAsync(
                string.Join(
                    options.Value.Csv.Delimiter,
                    "NORAD_ID",
                    "SATELLITE_NAME",
                    "CLASSIFICATION",
                    "INTERNATIONAL_DESIGNATOR",
                    "EPOCH_YEAR",
                    "EPOCH_DAY",
                    "SOURCE_NAME",
                    "DOWNLOADED_AT_UTC",
                    "RAW_FILE_PATH"));
        }

        foreach (var record in records.OrderBy(record => record.NoradId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(ToCsvLine(record, options.Value.Csv.Delimiter));
        }

        logger.LogInformation("Wrote staging CSV {Path}", path);
        return path;
    }

    private static string ToCsvLine(StagingSatelliteRecord record, string delimiter)
    {
        return string.Join(
            delimiter,
            record.NoradId.ToString(CultureInfo.InvariantCulture),
            Escape(record.SatelliteName, delimiter),
            Escape(record.Classification, delimiter),
            Escape(record.InternationalDesignator, delimiter),
            record.EpochYear.ToString(CultureInfo.InvariantCulture),
            record.EpochDay.ToString(CultureInfo.InvariantCulture),
            Escape(record.SourceName, delimiter),
            Escape(record.DownloadedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), delimiter),
            Escape(record.RawFilePath, delimiter));
    }

    private static string Escape(string value, string delimiter)
    {
        var requiresQuoting = value.Contains(delimiter, StringComparison.Ordinal)
            || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal)
            || value.Contains('\r', StringComparison.Ordinal);

        if (!requiresQuoting)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
