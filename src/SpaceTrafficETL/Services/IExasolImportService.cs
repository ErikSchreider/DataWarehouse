using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Services;

public interface IExasolImportService
{
    Task<IReadOnlyList<ExasolImportResult>> ReloadStagingTablesAsync(
        string? celesTrakCsvPath,
        string? ucsCsvPath,
        CancellationToken cancellationToken);
}
