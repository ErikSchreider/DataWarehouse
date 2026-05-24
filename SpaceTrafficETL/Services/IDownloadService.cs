using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Services;

public interface IDownloadService
{
    Task<IReadOnlyList<RawDataset>> DownloadCelesTrakAsync(CancellationToken cancellationToken);

    Task<RawDataset?> DownloadUcsAsync(CancellationToken cancellationToken);

    Task<RawDataset?> DownloadLaunchDataAsync(CancellationToken cancellationToken);
}
