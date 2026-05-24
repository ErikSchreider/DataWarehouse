namespace SpaceTrafficETL.Services;

public interface ISpaceTrafficEtlPipeline
{
    Task RunOnceAsync(CancellationToken cancellationToken);
}
