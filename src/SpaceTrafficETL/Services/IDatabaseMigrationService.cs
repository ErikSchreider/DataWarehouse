namespace SpaceTrafficETL.Services;

public interface IDatabaseMigrationService
{
    Task EnsureDatabaseAsync(CancellationToken cancellationToken);

    Task LoadWarehouseAsync(CancellationToken cancellationToken);
}
