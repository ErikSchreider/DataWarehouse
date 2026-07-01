using System.Globalization;
using System.Text.Json;
using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Services;

public sealed class CelesTrakJsonParserService(ILogger<CelesTrakJsonParserService> logger) : ICelesTrakJsonParserService
{
    public async Task<IReadOnlyList<CelesTrakObject>> ParseFileAsync(RawDataset dataset, CancellationToken cancellationToken)
    {
        var preview = await ReadPreviewAsync(dataset.RawFilePath, cancellationToken);
        if (!LooksLikeJson(preview))
        {
            logger.LogWarning(
                "Skipping CelesTrak dataset {SourceName} because the response is not JSON. File: {RawFilePath}. Response starts with: {ResponsePreview}",
                dataset.SourceName,
                dataset.RawFilePath,
                preview.Trim());

            return [];
        }

        await using var stream = File.OpenRead(dataset.RawFilePath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind is not JsonValueKind.Array)
        {
            logger.LogWarning("CelesTrak dataset {RawFilePath} is not a JSON array", dataset.RawFilePath);
            return [];
        }

        var sourceGroup = ExtractSourceGroup(dataset);
        var objects = new List<CelesTrakObject>();

        foreach (var item in document.RootElement.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var objectName = GetString(item, "OBJECT_NAME");
            var noradId = GetInt(item, "NORAD_CAT_ID");
            var epoch = GetDateTimeOffset(item, "EPOCH");
            var inclination = GetDouble(item, "INCLINATION");
            var eccentricity = GetDouble(item, "ECCENTRICITY");
            var meanMotion = GetDouble(item, "MEAN_MOTION");

            if (string.IsNullOrWhiteSpace(objectName)
                || noradId is null
                || epoch is null
                || inclination is null
                || eccentricity is null
                || meanMotion is null)
            {
                continue;
            }

            objects.Add(new CelesTrakObject(
                objectName,
                noradId.Value,
                epoch.Value,
                inclination.Value,
                eccentricity.Value,
                meanMotion.Value,
                sourceGroup,
                dataset.SourceName,
                dataset.DownloadedAt,
                dataset.RawFilePath));
        }

        logger.LogInformation(
            "Parsed {ObjectCount} CelesTrak GP JSON rows from {SourceName}",
            objects.Count,
            dataset.SourceName);

        return objects;
    }

    private static string ExtractSourceGroup(RawDataset dataset)
    {
        var query = dataset.SourceUri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in query)
        {
            var keyValue = part.Split('=', 2);
            if (keyValue.Length == 2 && keyValue[0].Equals("GROUP", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeSourceGroup(Uri.UnescapeDataString(keyValue[1]));
            }
        }

        return NormalizeSourceGroup(dataset.SourceName.Replace("celestrak-", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("-json", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim());
    }

    private static string NormalizeSourceGroup(string sourceGroup)
    {
        var normalized = sourceGroup.Trim().ToLowerInvariant();
        return normalized.Contains("debris", StringComparison.OrdinalIgnoreCase)
            ? "debris"
            : normalized;
    }

    private static async Task<string> ReadPreviewAsync(string path, CancellationToken cancellationToken)
    {
        var buffer = new char[512];
        using var reader = File.OpenText(path);
        var length = await reader.ReadBlockAsync(buffer, cancellationToken);
        return new string(buffer, 0, length);
    }

    private static bool LooksLikeJson(string preview)
    {
        foreach (var character in preview)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            return character is '[' or '{';
        }

        return false;
    }

    private static string? GetString(JsonElement item, string propertyName)
    {
        return item.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? GetInt(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind is JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind is JsonValueKind.String
            && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
                ? number
                : null;
    }

    private static double? GetDouble(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind is JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        return value.ValueKind is JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number)
                ? number
                : null;
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value) || value.ValueKind is not JsonValueKind.String)
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value.GetString(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsed)
                ? parsed.ToUniversalTime()
                : null;
    }
}
