using SpaceTrafficETL.Configuration;

namespace SpaceTrafficETL.Services;

public sealed class HttpDataSourceDownloader(HttpClient httpClient, ILogger<HttpDataSourceDownloader> logger)
    : IDataSourceDownloader
{
    public async Task<DownloadedFile> DownloadAsync(DataSourceOptions source, CancellationToken cancellationToken)
    {
        logger.LogInformation("Downloading {SourceName} from {SourceUrl}", source.Name, source.Url);

        using var response = await httpClient.GetAsync(source.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var memory = new MemoryStream();
        await response.Content.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        return new DownloadedFile(
            source,
            DateTimeOffset.UtcNow,
            response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
            memory);
    }
}
