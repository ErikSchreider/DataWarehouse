using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Services;

public interface ILaunchParserService
{
    Task<IReadOnlyList<LaunchRecord>> ParseFileAsync(RawDataset dataset, CancellationToken cancellationToken);
}
