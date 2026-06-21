using System.Data.Common;
using System.Data.Odbc;
using Microsoft.Extensions.Options;
using SpaceTrafficWeb.Configuration;

namespace SpaceTrafficWeb.Services;

public sealed class ExasolQueryService(IOptions<ExasolOptions> options, ILogger<ExasolQueryService> logger)
    : IExasolQueryService
{
    private const int MaxOpenAttempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken)
    {
        EnsureReadOnlyQuery(sql);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = RenderSql(sql);

        var rows = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    public async Task<T?> QuerySingleAsync<T>(
        string sql,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken)
    {
        var rows = await QueryAsync(sql, map, cancellationToken);
        return rows.FirstOrDefault();
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxOpenAttempts; attempt++)
        {
            var connection = new OdbcConnection(BuildConnectionString(options.Value));

            try
            {
                logger.LogDebug("Opening read-only Exasol connection to {Host}:{Port}", options.Value.Host, options.Value.Port);
                await connection.OpenAsync(cancellationToken);
                return connection;
            }
            catch (Exception exception) when (exception is not OperationCanceledException && attempt < MaxOpenAttempts)
            {
                await connection.DisposeAsync();
                logger.LogWarning(
                    exception,
                    "Exasol connection attempt {Attempt}/{MaxAttempts} failed; retrying in {RetryDelay}",
                    attempt,
                    MaxOpenAttempts,
                    RetryDelay);

                await Task.Delay(RetryDelay, cancellationToken);
            }
        }

        var finalConnection = new OdbcConnection(BuildConnectionString(options.Value));
        await finalConnection.OpenAsync(cancellationToken);
        return finalConnection;
    }

    private string RenderSql(string sql)
    {
        return sql.Replace("${SCHEMA}", QuoteIdentifier(options.Value.Schema), StringComparison.Ordinal);
    }

    private static void EnsureReadOnlyQuery(string sql)
    {
        var trimmed = sql.TrimStart();
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only read-only SELECT queries are allowed in SpaceTrafficWeb.");
        }
    }

    private static string BuildConnectionString(ExasolOptions exasol)
    {
        return string.Join(
            ';',
            $"Driver={{{EscapeDriverName(exasol.OdbcDriver)}}}",
            $"EXAHOST={exasol.Host}:{exasol.Port}",
            $"UID={exasol.Username}",
            $"PWD={exasol.Password}",
            $"CONNECTIONTIMEOUT={(int)exasol.ConnectionTimeout.TotalSeconds}");
    }

    private static string EscapeDriverName(string driverName)
    {
        return driverName.Replace("}", "}}", StringComparison.Ordinal);
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
