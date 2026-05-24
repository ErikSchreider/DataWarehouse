using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Services;

public sealed class UcsParserService(ILogger<UcsParserService> logger) : IUcsParserService
{
    public async Task<IReadOnlyList<UcsSatellite>> ParseFileAsync(RawDataset dataset, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(dataset.RawFilePath);
        using var reader = new StreamReader(stream);

        var firstLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return [];
        }

        if (firstLine.Contains("<html", StringComparison.OrdinalIgnoreCase)
            || firstLine.Contains("<!doctype", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "UCS dataset {RawFilePath} appears to be HTML, not CSV/TSV. Configure SatelliteDatabaseDownloadUrl to a downloadable tabular file.",
                dataset.RawFilePath);
            return [];
        }

        stream.Position = 0;
        reader.DiscardBufferedData();

        using var csv = new CsvReader(reader, CreateConfiguration(firstLine));
        await csv.ReadAsync();
        csv.ReadHeader();

        var satellites = new List<UcsSatellite>();
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var record = csv.GetRecord<dynamic>() as IDictionary<string, object>;
            if (record is null)
            {
                continue;
            }

            var normalized = record.ToDictionary(
                item => NormalizeHeader(item.Key),
                item => item.Value?.ToString(),
                StringComparer.OrdinalIgnoreCase);

            var name = GetValue(normalized, "current official name of satellite", "name of satellite", "satellite name", "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            satellites.Add(new UcsSatellite(
                name,
                ParseNullableInt(GetValue(normalized, "norad number", "norad id", "norad_id")),
                GetValue(normalized, "operator owner", "operator", "owner operator"),
                GetValue(normalized, "country org of un registry", "country", "users"),
                GetValue(normalized, "purpose"),
                GetValue(normalized, "class of orbit", "orbit class"),
                GetValue(normalized, "operational status", "status"),
                ParseDate(GetValue(normalized, "date of launch", "launch date")),
                dataset.SourceName,
                dataset.DownloadedAt,
                dataset.RawFilePath));
        }

        logger.LogInformation("Parsed {SatelliteCount} UCS satellite rows from {RawFilePath}", satellites.Count, dataset.RawFilePath);
        return satellites;
    }

    private static CsvConfiguration CreateConfiguration(string firstLine)
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = DetectDelimiter(firstLine),
            BadDataFound = null,
            MissingFieldFound = null,
            HeaderValidated = null,
            TrimOptions = TrimOptions.Trim
        };
    }

    private static string DetectDelimiter(string firstLine)
    {
        var delimiters = new[] { "\t", ";", "," };
        return delimiters
            .OrderByDescending(delimiter => firstLine.Count(character => character.ToString() == delimiter))
            .First();
    }

    private static string NormalizeHeader(string value)
    {
        var normalized = value.Replace('_', ' ').Replace('/', ' ').Replace('-', ' ');
        return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim().ToLowerInvariant();
    }

    private static string? GetValue(IDictionary<string, string?> values, params string[] names)
    {
        foreach (var name in names)
        {
            if (values.TryGetValue(NormalizeHeader(name), out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static int? ParseNullableInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateTime)
            ? DateOnly.FromDateTime(dateTime)
            : null;
    }
}
