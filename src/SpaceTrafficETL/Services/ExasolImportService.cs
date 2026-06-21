using System.Globalization;
using System.Data.Common;
using CsvHelper;
using CsvHelper.Configuration;
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
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await DeleteFromAsync(connection, transaction, "stg_celestrak_objects", cancellationToken);
            await DeleteFromAsync(connection, transaction, "stg_ucs_satellites", cancellationToken);
            await DeleteFromAsync(connection, transaction, "stg_launch_events", cancellationToken);

            var results = new List<ExasolImportResult>();

            if (!string.IsNullOrWhiteSpace(celesTrakCsvPath))
            {
                results.Add(await ImportCsvAsync(connection, transaction, "stg_celestrak_objects", celesTrakCsvPath, cancellationToken));
            }

            if (!string.IsNullOrWhiteSpace(ucsCsvPath))
            {
                results.Add(await ImportCsvAsync(connection, transaction, "stg_ucs_satellites", ucsCsvPath, cancellationToken));
            }

            if (!string.IsNullOrWhiteSpace(launchCsvPath))
            {
                results.Add(await ImportCsvAsync(connection, transaction, "stg_launch_events", launchCsvPath, cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);
            return results;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task DeleteFromAsync(
        DbConnection connection,
        DbTransaction transaction,
        string tableName,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Clearing Exasol staging table {TableName}", tableName);
        await ExecuteNonQueryAsync(connection, transaction, $"DELETE FROM {QualifiedTable(tableName)}", cancellationToken);
    }

    private async Task<ExasolImportResult> ImportCsvAsync(
        DbConnection connection,
        DbTransaction transaction,
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

            var importedRows = await InsertCsvRowsAsync(connection, transaction, tableName, csvPath, cancellationToken);
            logger.LogInformation("Imported {RowCount} rows into {TableName}", rowCount, tableName);

            return new ExasolImportResult(tableName, csvPath, importedRows);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Error importing CSV {CsvPath} into Exasol table {TableName}", csvPath, tableName);
            throw;
        }
    }

    private async Task<long> InsertCsvRowsAsync(
        DbConnection connection,
        DbTransaction transaction,
        string tableName,
        string csvPath,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(csvPath);
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CreateCsvConfiguration());

        if (!await csv.ReadAsync())
        {
            return 0;
        }

        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];
        if (headers.Length == 0)
        {
            return 0;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = BuildInsertStatement(tableName, headers);

        foreach (var _ in headers)
        {
            command.Parameters.Add(command.CreateParameter());
        }

        var rowCount = 0L;
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (var index = 0; index < headers.Length; index++)
            {
                var value = csv.GetField(index);
                command.Parameters[index].Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
            rowCount++;
        }

        return rowCount;
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

    private async Task<int> ExecuteNonQueryAsync(
        DbConnection connection,
        DbTransaction transaction,
        string statement,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = statement;
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private CsvConfiguration CreateCsvConfiguration()
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = options.Value.Csv.Delimiter,
            HasHeaderRecord = options.Value.Csv.IncludeHeader,
            BadDataFound = null,
            MissingFieldFound = null,
            HeaderValidated = null,
            TrimOptions = TrimOptions.Trim
        };
    }

    private string BuildInsertStatement(string tableName, IReadOnlyList<string> columns)
    {
        var columnList = string.Join(", ", columns.Select(QuoteIdentifier));
        var parameterList = string.Join(", ", columns.Select(_ => "?"));
        return $"INSERT INTO {QualifiedTable(tableName)} ({columnList}) VALUES ({parameterList})";
    }

    private string QualifiedTable(string tableName)
    {
        return $"{QuoteIdentifier(options.Value.Exasol.Schema)}.{QuoteIdentifier(tableName)}";
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

}
