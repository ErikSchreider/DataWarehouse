using SpaceTrafficWeb.Models;

namespace SpaceTrafficWeb.Services;

public interface IOrbitVisualizationService
{
    Task<IReadOnlyList<OrbitObjectPoint>> GetOrbitObjectsAsync(CancellationToken cancellationToken);
}
