using SpaceTrafficWeb.Models;

namespace SpaceTrafficWeb.Services;

public interface IDashboardService
{
    Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<OrbitClassCount>> GetObjectsByOrbitClassAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ObjectTypeAltitude>> GetAverageAltitudeByObjectTypeAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SourceSummary>> GetSourcesAsync(CancellationToken cancellationToken);
}
