using SpaceTrafficETL.Configuration;

namespace SpaceTrafficETL.Services;

public interface IDataSourceDownloader
{
    Task<DownloadedFile> DownloadAsync(DataSourceOptions source, CancellationToken cancellationToken);
}

public sealed record DownloadedFile(
    DataSourceOptions Source,
    DateTimeOffset DownloadedAt,
    string ContentType,
    Stream Content);
