using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Services;

public interface IRawFileStore
{
    Task<RawDataset> SaveAsync(DownloadedFile file, CancellationToken cancellationToken);
}
