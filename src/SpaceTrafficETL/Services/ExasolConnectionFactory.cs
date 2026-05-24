using System.Data.Common;
using System.Data.Odbc;
using Microsoft.Extensions.Options;
using SpaceTrafficETL.Configuration;

namespace SpaceTrafficETL.Services;

public sealed class ExasolConnectionFactory(IOptions<SpaceTrafficOptions> options, ILogger<ExasolConnectionFactory> logger)
    : IExasolConnectionFactory
{
    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new OdbcConnection(BuildConnectionString(options.Value.Exasol));

        logger.LogDebug("Opening Exasol ODBC connection to {Host}:{Port}", options.Value.Exasol.Host, options.Value.Exasol.Port);
        await connection.OpenAsync(cancellationToken);

        return connection;
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
