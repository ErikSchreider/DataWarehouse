using System.Data.Common;
using System.Data.Odbc;
using Microsoft.Extensions.Options;
using SpaceTrafficETL.Configuration;

namespace SpaceTrafficETL.Services;

public sealed class ExasolConnectionFactory(IOptions<SpaceTrafficOptions> options, ILogger<ExasolConnectionFactory> logger)
    : IExasolConnectionFactory
{
    private const int MaxOpenAttempts = 30;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxOpenAttempts; attempt++)
        {
            var connection = new OdbcConnection(BuildConnectionString(options.Value.Exasol));

            try
            {
                logger.LogDebug("Opening Exasol ODBC connection to {Host}:{Port}", options.Value.Exasol.Host, options.Value.Exasol.Port);
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

        var finalConnection = new OdbcConnection(BuildConnectionString(options.Value.Exasol));
        await finalConnection.OpenAsync(cancellationToken);
        return finalConnection;
    }

    private static string BuildConnectionString(ExasolOptions exasol)
    {
        var parts = new List<string>
        {
            $"Driver={{{EscapeDriverName(exasol.OdbcDriver)}}}",
            $"EXAHOST={exasol.Host}:{exasol.Port}",
            $"UID={exasol.Username}",
            $"PWD={exasol.Password}",
            $"CONNECTIONTIMEOUT={(int)exasol.ConnectionTimeout.TotalSeconds}"
        };

        if (!string.IsNullOrWhiteSpace(exasol.Fingerprint))
        {
            parts.Add($"FINGERPRINT={exasol.Fingerprint}");
        }

        return string.Join(';', parts);
    }

    private static string EscapeDriverName(string driverName)
    {
        return driverName.Replace("}", "}}", StringComparison.Ordinal);
    }
}
