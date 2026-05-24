namespace SpaceTrafficETL.Services;

public interface IEtlOrchestrator
{
    Task RunOnceAsync(CancellationToken cancellationToken);
}
