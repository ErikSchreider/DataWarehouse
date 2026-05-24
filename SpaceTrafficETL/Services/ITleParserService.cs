using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Services;

public interface ITleParserService
{
    Task<IReadOnlyList<TleObject>> ParseFileAsync(string path, CancellationToken cancellationToken);
}
