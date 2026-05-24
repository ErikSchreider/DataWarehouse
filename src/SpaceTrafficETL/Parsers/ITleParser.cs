using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Parsers;

public interface ITleParser
{
    Task<IReadOnlyList<TleRecord>> ParseFileAsync(string path, CancellationToken cancellationToken);
}
