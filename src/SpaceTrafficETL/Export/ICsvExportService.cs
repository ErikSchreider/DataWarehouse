using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Export;

public interface ICsvExportService
{
    Task<string> ExportCelesTrakObjectsAsync(
        IReadOnlyCollection<CelesTrakObject> objects,
        DateTimeOffset runTimestamp,
        CancellationToken cancellationToken);

    Task<string> ExportUcsSatellitesAsync(
        IReadOnlyCollection<UcsSatellite> satellites,
        DateTimeOffset runTimestamp,
        CancellationToken cancellationToken);

    Task<string> ExportLaunchesAsync(
        IReadOnlyCollection<LaunchRecord> launches,
        DateTimeOffset runTimestamp,
        CancellationToken cancellationToken);
}
