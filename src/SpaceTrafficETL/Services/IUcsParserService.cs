using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Services;

public interface IUcsParserService
{
    Task<IReadOnlyList<UcsSatellite>> ParseFileAsync(RawDataset dataset, CancellationToken cancellationToken);
}
