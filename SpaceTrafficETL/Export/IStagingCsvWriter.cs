using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Export;

public interface IStagingCsvWriter
{
    Task<string> WriteSatelliteStageAsync(
        IReadOnlyCollection<StagingSatelliteRecord> records,
        DateTimeOffset runTimestamp,
        CancellationToken cancellationToken);
}
