using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SpaceTrafficETL.Configuration;

namespace SpaceTrafficETL.Services;

public sealed class DatabaseMigrationService(
    IExasolConnectionFactory connectionFactory,
    IOptions<SpaceTrafficOptions> options,
    IHostEnvironment environment,
    ILogger<DatabaseMigrationService> logger)
    : IDatabaseMigrationService
{
    private static readonly Regex CreateTableRegex = new(
        "^CREATE\\s+TABLE\\s+\"?(?<table>[A-Za-z0-9_]+)\"?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] BootstrapScripts =
    [
        "001_create_schema.sql",
        "002_create_staging_tables.sql",
        "003_create_dwh_tables.sql"
    ];

    private static readonly string[] WarehouseLoadScripts =
    [
        "004_load_dimensions.sql",
        "005_load_facts.sql",
        "006_seed_recent_four_weeks.sql"
    ];

    public async Task EnsureDatabaseAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Checking Exasol schema and table structure");
        await ExecuteScriptsAsync(BootstrapScripts, cancellationToken);
        logger.LogInformation("Exasol schema and table structure are ready");
    }

    public async Task LoadWarehouseAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Updating Exasol dimension and fact tables from staging tables");
        await ExecuteScriptsAsync(WarehouseLoadScripts, cancellationToken);
        logger.LogInformation("Exasol dimension and fact load scripts completed");
    }

    private async Task ExecuteScriptsAsync(IEnumerable<string> scriptNames, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);

        foreach (var scriptName in scriptNames)
        {
            var scriptPath = Path.Combine(environment.ContentRootPath, "Sql", scriptName);
            var sql = await File.ReadAllTextAsync(scriptPath, cancellationToken);
            var renderedSql = RenderSql(sql);
            var statements = SplitStatements(renderedSql).ToArray();

            logger.LogInformation("Executing SQL script {ScriptName} with {StatementCount} statements", scriptName, statements.Length);

            foreach (var statement in statements)
            {
                if (await ShouldSkipStatementAsync(connection, statement, cancellationToken))
                {
                    continue;
                }

                await ExecuteNonQueryAsync(connection, statement, cancellationToken);
            }
        }
    }

    private async Task<bool> ShouldSkipStatementAsync(
        DbConnection connection,
        string statement,
        CancellationToken cancellationToken)
    {
        var normalized = statement.TrimStart();

        if (normalized.StartsWith("CREATE SCHEMA", StringComparison.OrdinalIgnoreCase)
            && await SchemaExistsAsync(connection, cancellationToken))
        {
            logger.LogInformation("Schema {SchemaName} already exists; skipping CREATE SCHEMA", options.Value.Exasol.Schema);
            return true;
        }

        var tableMatch = CreateTableRegex.Match(normalized);
        if (!tableMatch.Success)
        {
            return false;
        }

        var tableName = tableMatch.Groups["table"].Value;
        if (!await TableExistsAsync(connection, tableName, cancellationToken))
        {
            return false;
        }

        logger.LogInformation("Table {SchemaName}.{TableName} already exists; skipping CREATE TABLE", options.Value.Exasol.Schema, tableName);
        return true;
    }

    private async Task<bool> SchemaExistsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        const string statement = """
            SELECT COUNT(*)
            FROM EXA_ALL_SCHEMAS
            WHERE SCHEMA_NAME = ?
            """;

        return await CountMetadataRowsAsync(connection, statement, options.Value.Exasol.Schema, cancellationToken) > 0;
    }

    private async Task<bool> TableExistsAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        const string statement = """
            SELECT COUNT(*)
            FROM EXA_ALL_TABLES
            WHERE TABLE_SCHEMA = ?
              AND TABLE_NAME = ?
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = statement;

        var schemaParameter = command.CreateParameter();
        schemaParameter.Value = options.Value.Exasol.Schema;
        command.Parameters.Add(schemaParameter);

        var tableParameter = command.CreateParameter();
        tableParameter.Value = tableName;
        command.Parameters.Add(tableParameter);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result) > 0;
    }

    private static async Task<long> CountMetadataRowsAsync(
        DbConnection connection,
        string statement,
        string value,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = statement;

        var parameter = command.CreateParameter();
        parameter.Value = value;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    private string RenderSql(string sql)
    {
        return sql.Replace("${SCHEMA}", QuoteIdentifier(options.Value.Exasol.Schema), StringComparison.Ordinal);
    }

    private static async Task<int> ExecuteNonQueryAsync(
        DbConnection connection,
        string statement,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = statement;
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static IEnumerable<string> SplitStatements(string sql)
    {
        return sql
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(statement => !string.IsNullOrWhiteSpace(statement));
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
