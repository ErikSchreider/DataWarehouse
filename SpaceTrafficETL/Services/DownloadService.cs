using Microsoft.Extensions.Options;
using SpaceTrafficETL.Configuration;
using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Services;

public sealed class DownloadService(
    IHttpClientFactory httpClientFactory,
    IOptions<SpaceTrafficOptions> options,
    ILogger<DownloadService> logger)
    : IDownloadService
{
    public const string HttpClientName = "SpaceTrafficDownloads";

    public async Task<IReadOnlyList<RawDataset>> DownloadCelesTrakAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.CelesTrak.Enabled)
        {
            logger.LogInformation("CelesTrak downloads are disabled");
            return [];
        }

        var downloads = new[]
        {
            new DownloadRequest(
                "celestrak-active-tle",
                DataSourceKind.Tle,
                options.Value.CelesTrak.ActiveSatellitesTleUrl,
                options.Value.CelesTrak.RawFileExtension),
            new DownloadRequest(
                "celestrak-stations-tle",
                DataSourceKind.Tle,
                options.Value.CelesTrak.StationsTleUrl,
                options.Value.CelesTrak.RawFileExtension)
        };

        var datasets = new List<RawDataset>(downloads.Length);
        foreach (var download in downloads)
        {
            try
            {
                datasets.Add(await DownloadAndStoreAsync(download, cancellationToken));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Failed to download CelesTrak source {SourceName}", download.SourceName);
            }
        }

        return datasets;
    }

    public async Task<RawDataset?> DownloadUcsAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Ucs.Enabled)
        {
            logger.LogInformation("UCS downloads are disabled");
            return null;
        }

        return await DownloadAndStoreAsync(
            new DownloadRequest(
                "ucs-satellite-database",
                DataSourceKind.UcsSatelliteDatabase,
                options.Value.Ucs.SatelliteDatabaseDownloadUrl,
                options.Value.Ucs.RawFileExtension),
            cancellationToken);
    }

    public async Task<RawDataset?> DownloadLaunchDataAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.LaunchData.Enabled)
        {
            logger.LogInformation("Launch data downloads are disabled");
            return null;
        }

        return await DownloadAndStoreAsync(
            new DownloadRequest(
                "spacedevs-upcoming-launches",
                DataSourceKind.SpaceDevsLaunches,
                options.Value.LaunchData.UpcomingLaunchesUrl,
                options.Value.LaunchData.RawFileExtension),
            cancellationToken);
    }

    private async Task<RawDataset> DownloadAndStoreAsync(DownloadRequest request, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= options.Value.Etl.MaxDownloadRetries; attempt++)
        {
            try
            {
                return await DownloadAndStoreAttemptAsync(request, attempt, cancellationToken);
            }
            catch (Exception exception) when (ShouldRetry(exception, attempt))
            {
                logger.LogWarning(
                    exception,
                    "Download attempt {Attempt}/{MaxAttempts} failed for {SourceName}; retrying after {RetryDelay}",
                    attempt,
                    options.Value.Etl.MaxDownloadRetries,
                    request.SourceName,
                    options.Value.Etl.DownloadRetryDelay);

                await Task.Delay(options.Value.Etl.DownloadRetryDelay, cancellationToken);
            }
        }

        throw new InvalidOperationException($"Download failed for {request.SourceName} after retry exhaustion.");
    }

    private async Task<RawDataset> DownloadAndStoreAttemptAsync(
        DownloadRequest request,
        int attempt,
        CancellationToken cancellationToken)
    {
        var httpClient = httpClientFactory.CreateClient(HttpClientName);
        var downloadedAt = DateTimeOffset.UtcNow;
        var rawDirectory = GetRawDirectory(downloadedAt);
        Directory.CreateDirectory(rawDirectory);

        var fileName = $"{downloadedAt:yyyyMMddTHHmmssfffZ}_{ToSafePathSegment(request.SourceName)}{request.RawFileExtension}";
        var rawFilePath = Path.Combine(rawDirectory, fileName);

        logger.LogInformation(
            "Downloading {SourceName} from {SourceUrl}; attempt {Attempt}/{MaxAttempts}",
            request.SourceName,
            request.Url,
            attempt,
            options.Value.Etl.MaxDownloadRetries);

        using var response = await httpClient.GetAsync(request.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var output = File.Create(rawFilePath);
        await response.Content.CopyToAsync(output, cancellationToken);

        logger.LogInformation("Stored {SourceName} raw file at {RawFilePath}", request.SourceName, rawFilePath);

        return new RawDataset(
            request.SourceName,
            request.Kind,
            request.Url,
            downloadedAt,
            response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
            rawFilePath);
    }

    private bool ShouldRetry(Exception exception, int attempt)
    {
        return attempt < options.Value.Etl.MaxDownloadRetries
            && exception is HttpRequestException or IOException or TaskCanceledException;
    }

    private string GetRawDirectory(DateTimeOffset downloadedAt)
    {
        return Path.Combine(
            options.Value.DataDirectories.StorageRoot,
            options.Value.DataDirectories.Raw,
            downloadedAt.ToString("yyyy-MM-dd"));
    }

    private static string ToSafePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(character => invalid.Contains(character) ? '_' : character));
    }

    private sealed record DownloadRequest(
        string SourceName,
        DataSourceKind Kind,
        Uri Url,
        string RawFileExtension);
}
