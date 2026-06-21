using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
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

        if (firstLine.StartsWith("PK", StringComparison.Ordinal))
        {
            return await ParseXlsxAsync(dataset, cancellationToken);
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

            var normalized = BuildNormalizedValues(record.Select(item =>
                new KeyValuePair<string, string?>(item.Key, item.Value?.ToString())));

            var name = GetValue(normalized, "current official name of satellite", "name of satellite", "satellite name", "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            satellites.Add(new UcsSatellite(
                name,
                ParseNullableInt(GetValue(normalized, "norad number", "norad id", "norad_id")),
                GetValue(normalized, "operator owner", "operator owner", "operator", "owner operator"),
                GetValue(normalized, "country org of un registry", "country", "users", "country"),
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

    private async Task<IReadOnlyList<UcsSatellite>> ParseXlsxAsync(RawDataset dataset, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(dataset.RawFilePath);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
        if (sheetEntry is null)
        {
            logger.LogWarning("UCS workbook {RawFilePath} does not contain xl/worksheets/sheet1.xml", dataset.RawFilePath);
            return [];
        }

        var sharedStrings = await ReadSharedStringsAsync(archive, cancellationToken);
        await using var sheetStream = sheetEntry.Open();
        var sheet = await XDocument.LoadAsync(sheetStream, LoadOptions.None, cancellationToken);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        var rows = sheet.Descendants(ns + "row")
            .Select(row => row.Elements(ns + "c")
                .Select(cell => new
                {
                    Column = GetColumnName(cell.Attribute("r")?.Value ?? string.Empty),
                    Value = ReadCellValue(cell, sharedStrings, ns)
                })
                .Where(cell => !string.IsNullOrWhiteSpace(cell.Column))
                .ToDictionary(cell => cell.Column, cell => cell.Value))
            .Where(row => row.Count > 0)
            .ToArray();

        if (rows.Length == 0)
        {
            return [];
        }

        var headers = rows[0].ToDictionary(
            item => item.Key,
            item => NormalizeHeader(item.Value ?? string.Empty),
            StringComparer.OrdinalIgnoreCase);

        var satellites = new List<UcsSatellite>();
        foreach (var row in rows.Skip(1))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalized = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in headers.Where(header => !string.IsNullOrWhiteSpace(header.Value)))
            {
                row.TryGetValue(header.Key, out var value);
                AddNormalizedValue(normalized, header.Value, value);
            }

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

        logger.LogInformation("Parsed {SatelliteCount} UCS satellite rows from Excel workbook {RawFilePath}", satellites.Count, dataset.RawFilePath);
        return satellites;
    }

    private static async Task<IReadOnlyList<string>> ReadSharedStringsAsync(
        ZipArchive archive,
        CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        await using var stream = entry.Open();
        var document = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return document.Descendants(ns + "si")
            .Select(item => string.Concat(item.Descendants(ns + "t").Select(text => text.Value)))
            .ToArray();
    }

    private static string? ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings, XNamespace ns)
    {
        var value = cell.Element(ns + "v")?.Value;
        if (value is null)
        {
            return cell.Element(ns + "is")?.Descendants(ns + "t").FirstOrDefault()?.Value;
        }

        if (cell.Attribute("t")?.Value == "s"
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedStringIndex)
            && sharedStringIndex >= 0
            && sharedStringIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedStringIndex];
        }

        return value;
    }

    private static string GetColumnName(string cellReference)
    {
        return string.Concat(cellReference.TakeWhile(char.IsLetter));
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

    private static Dictionary<string, string?> BuildNormalizedValues(IEnumerable<KeyValuePair<string, string?>> values)
    {
        var normalized = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            AddNormalizedValue(normalized, value.Key, value.Value);
        }

        return normalized;
    }

    private static void AddNormalizedValue(IDictionary<string, string?> values, string key, string? value)
    {
        var normalizedKey = NormalizeHeader(key);
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return;
        }

        if (!values.TryGetValue(normalizedKey, out var existingValue))
        {
            values.Add(normalizedKey, value);
            return;
        }

        if (string.IsNullOrWhiteSpace(existingValue) && !string.IsNullOrWhiteSpace(value))
        {
            values[normalizedKey] = value;
        }
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
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serialDate)
            && serialDate > 1)
        {
            return DateOnly.FromDateTime(DateTime.FromOADate(serialDate));
        }

        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateTime)
            ? DateOnly.FromDateTime(dateTime)
            : null;
    }
}
