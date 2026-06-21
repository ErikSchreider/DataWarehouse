using SpaceTrafficWeb.Models;

namespace SpaceTrafficWeb.Services;

public interface IAnalyticsService
{
    Task<AnalyticsDashboard> GetAnalyticsAsync(CancellationToken cancellationToken);
}
