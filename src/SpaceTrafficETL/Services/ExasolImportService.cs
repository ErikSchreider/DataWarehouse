using System.Data.Common;
using Microsoft.Extensions.Options;
using SpaceTrafficETL.Configuration;
using SpaceTrafficETL.Models;

namespace SpaceTrafficETL.Services;

public sealed class ExasolImportService(
    IExasolConnectionFactory connectionFactory,
    IOptions<SpaceTrafficOptions> options,
    ILogger<ExasolImportService> logger)
    : IExasolImportService
{
    public async Task<IReadOnlyList<ExasolImportResult>> ReloadStagingTablesAsync(
        string? celesTrakCsvPath,
        string? ucsCsvPath,
        string? launchCsvPath,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        await DeleteFromAsync(connection, "stg_celestrak_objects", cancellationToken);
        await DeleteFromAsync(connection, "stg_ucs_satellites", cancellationToken);
        await DeleteFromAsync(connection, "stg_launch_events", cancellationToken);

        var results = new List<ExasolImportResult>();

        if (!string.IsNullOrWhiteSpace(celesTrakCsvPath))
        {
            results.Add(await ImportCsvAsync(connection, "stg_celestrak_objects", celesTrakCsvPath, cancellationToken));
        }

        if (!string.IsNullOrWhiteSpace(ucsCsvPath))
        {
            results.Add(await ImportCsvAsync(connection, "stg_ucs_satellites", ucsCsvPath, cancellationToken));
        }

        if (!string.IsNullOrWhiteSpace(launchCsvPath))
        {
            results.Add(await ImportCsvAsync(connection, "stg_launch_events", launchCsvPath, cancellationToken));
        }

        return results;
    }

    private async Task DeleteFromAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        logger.LogInformation("Clearing Exasol staging table {TableName}", tableName);
        await ExecuteNonQueryAsync(connection, $"DELETE FROM {QualifiedTable(tableName)}", cancellationToken);
    }

    private async Task<ExasolImportResult> ImportCsvAsync(
        DbConnection connection,
        string tableName,
        string csvPath,
        CancellationToken cancellationToken)
    {
        var rowCount = await CountDataRowsAsync(csvPath, cancellationToken);

        if (rowCount == 0)
        {
            logger.LogInformation("CSV {CsvPath} contains no data rows; skipping import for {TableName}", csvPath, tableName);
            return new ExasolImportResult(tableName, csvPath, 0);
        }

        try
        {
            logger.LogInformation("Importing {RowCount} rows from {CsvPath} into {TableName}", rowCount, csvPath, tableName);

            var statement = $"""
                IMPORT INTO {QualifiedTable(tableName)}
                FROM LOCAL CSV FILE '{EscapeSqlLiteral(csvPath)}'
                COLUMN SEPARATOR = '{EscapeSqlLiteral(options.Value.Csv.Delimiter)}'
                COLUMN DELIMITER = '"'
                SKIP = 1
                """;

            await ExecuteNonQueryAsync(connection, statement, cancellationToken);
            logger.LogInformation("Imported {RowCount} rows into {TableName}", rowCount, tableName);

            return new ExasolImportResult(tableName, csvPath, rowCount);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Error importing CSV {CsvPath} into Exasol table {TableName}", csvPath, tableName);
            throw;
        }
    }

    private async Task<long> CountDataRowsAsync(string csvPath, CancellationToken cancellationToken)
    {
        var count = 0L;
        await using var stream = File.OpenRead(csvPath);
        using var reader = new StreamReader(stream);

        var isHeader = true;
        while (await reader.ReadLineAsync(cancellationToken) is not null)
        {
            if (isHeader && options.Value.Csv.IncludeHeader)
            {
                isHeader = false;
                continue;
            }

            isHeader = false;
            count++;
        }

        return count;
    }

    private async Task<int> ExecuteNonQueryAsync(DbConnection connection, string statement, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = statement;
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private string QualifiedTable(string tableName)
    {
        return $"{QuoteIdentifier(options.Value.Exasol.Schema)}.{QuoteIdentifier(tableName)}";
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string EscapeSqlLiteral(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }
}
