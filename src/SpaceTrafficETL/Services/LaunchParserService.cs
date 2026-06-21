using System.Globalization;
using System.Text.Json;
using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Services;

public sealed class LaunchParserService(ILogger<LaunchParserService> logger) : ILaunchParserService
{
    public async Task<IReadOnlyList<LaunchRecord>> ParseFileAsync(RawDataset dataset, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(dataset.RawFilePath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var launchElements = GetLaunchElements(document.RootElement);
        var records = new List<LaunchRecord>();

        foreach (var launch in launchElements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var launchName = GetString(launch, "name") ?? "UNKNOWN";
            var launchDate = GetDateTimeOffset(launch, "net")
                ?? GetDateTimeOffset(launch, "window_start");
            var provider = GetNestedString(launch, ["launch_service_provider", "name"]);
            var rocketName = GetNestedString(launch, ["rocket", "configuration", "full_name"])
                ?? GetNestedString(launch, ["rocket", "configuration", "name"]);
            var launchStatus = GetNestedString(launch, ["status", "name"]);
            var country = GetNestedString(launch, ["pad", "location", "country_code"])
                ?? GetNestedString(launch, ["pad", "country_code"]);
            var payloadCount = GetPayloadCount(launch);
            var launchId = GetString(launch, "id") ?? BuildFallbackLaunchId(launchName, launchDate, provider);

            records.Add(new LaunchRecord(
                launchId,
                launchName,
                launchDate,
                provider,
                rocketName,
                country,
                launchStatus,
                payloadCount,
                dataset.SourceName,
                dataset.DownloadedAt,
                dataset.RawFilePath));
        }

        logger.LogInformation("Parsed {LaunchCount} launch rows from {SourceName}", records.Count, dataset.SourceName);
        return records;
    }

    private static IEnumerable<JsonElement> GetLaunchElements(JsonElement root)
    {
        if (root.ValueKind is JsonValueKind.Array)
        {
            return root.EnumerateArray().ToArray();
        }

        if (root.ValueKind is JsonValueKind.Object
            && root.TryGetProperty("results", out var results)
            && results.ValueKind is JsonValueKind.Array)
        {
            return results.EnumerateArray().ToArray();
        }

        return [];
    }

    private static int? GetPayloadCount(JsonElement launch)
    {
        if (TryGetNestedElement(launch, ["rocket", "spacecraft_stage", "payloads"], out var payloads)
            && payloads.ValueKind is JsonValueKind.Array)
        {
            return payloads.GetArrayLength();
        }

        return null;
    }

    private static string BuildFallbackLaunchId(string launchName, DateTimeOffset? launchDate, string? provider)
    {
        var key = $"{launchName}|{launchDate:O}|{provider}".ToLowerInvariant();
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(key)));
    }

    private static string? GetString(JsonElement item, string propertyName)
    {
        return item.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.String
            ? value.GetString()
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

    private static string? GetNestedString(JsonElement root, IReadOnlyList<string> path)
    {
        return TryGetNestedElement(root, path, out var value) && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool TryGetNestedElement(JsonElement root, IReadOnlyList<string> path, out JsonElement value)
    {
        value = root;
        foreach (var segment in path)
        {
            if (value.ValueKind is not JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                return false;
            }
        }

        return true;
    }
}
