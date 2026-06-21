using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Services;

public interface ICelesTrakJsonParserService
{
    Task<IReadOnlyList<CelesTrakObject>> ParseFileAsync(RawDataset dataset, CancellationToken cancellationToken);
}
