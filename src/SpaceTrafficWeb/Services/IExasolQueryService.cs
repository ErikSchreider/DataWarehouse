using System.Data.Common;

namespace SpaceTrafficWeb.Services;

public interface IExasolQueryService
{
    Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken);

    Task<T?> QuerySingleAsync<T>(
        string sql,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken);
}
