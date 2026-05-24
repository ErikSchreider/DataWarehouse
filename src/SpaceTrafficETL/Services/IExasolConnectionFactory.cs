using System.Data.Common;

namespace SpaceTrafficETL.Services;

public interface IExasolConnectionFactory
{
    Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}
