using Microsoft.Extensions.Options;
using SpaceTrafficETL.Configuration;
using SpaceTrafficETL.Export;
using SpaceTrafficETL.Parsers;
using SpaceTrafficETL.Services;
using SpaceTrafficETL.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddInMemoryCollection(BuildEnvironmentOverrides(builder.Configuration));

builder.Services
    .AddOptions<SpaceTrafficOptions>()
    .Bind(builder.Configuration.GetSection(SpaceTrafficOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.Etl.Interval > TimeSpan.Zero, "ETL interval must be greater than zero.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Exasol.Host), "Exasol host is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Exasol.Schema), "Exasol schema is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Exasol.Username), "Exasol user is required.")
    .Validate(
        options => options.CelesTrak.Enabled || options.Ucs.Enabled || options.LaunchData.Enabled,
        "At least one data source must be enabled.")
    .ValidateOnStart();

builder.Services.AddHttpClient(DownloadService.HttpClientName, (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<SpaceTrafficOptions>>().Value;
    client.Timeout = options.Etl.DownloadTimeout;
    client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
});

builder.Services.AddSingleton<IDownloadService, DownloadService>();
builder.Services.AddSingleton<ICelesTrakJsonParserService, CelesTrakJsonParserService>();
builder.Services.AddSingleton<ILaunchParserService, LaunchParserService>();
builder.Services.AddSingleton<ITleParserService, TleParserService>();
builder.Services.AddSingleton<IUcsParserService, UcsParserService>();
builder.Services.AddHttpClient<IDataSourceDownloader, HttpDataSourceDownloader>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<SpaceTrafficOptions>>().Value;
    client.Timeout = options.Etl.DownloadTimeout;
    client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
});

builder.Services.AddSingleton<IRawFileStore, FileSystemRawFileStore>();
builder.Services.AddSingleton<ITleParser, TleParser>();
builder.Services.AddSingleton<ICsvExportService, CsvExportService>();
builder.Services.AddSingleton<IStagingCsvWriter, StagingCsvWriter>();
builder.Services.AddSingleton<IExasolConnectionFactory, ExasolConnectionFactory>();
builder.Services.AddSingleton<IDatabaseMigrationService, DatabaseMigrationService>();
builder.Services.AddSingleton<IExasolImportService, ExasolImportService>();
builder.Services.AddSingleton<IEtlOrchestrator, EtlOrchestrator>();
builder.Services.AddSingleton<ISpaceTrafficEtlPipeline, SpaceTrafficEtlPipeline>();
builder.Services.AddHostedService<SpaceTrafficWorker>();

await builder.Build().RunAsync();

static Dictionary<string, string?> BuildEnvironmentOverrides(IConfiguration configuration)
{
    var overrides = new Dictionary<string, string?>();

    AddIfPresent(overrides, "SpaceTraffic:Exasol:Host", configuration["EXASOL_HOST"]);
    AddIfPresent(overrides, "SpaceTraffic:Exasol:Port", configuration["EXASOL_PORT"]);
    AddIfPresent(overrides, "SpaceTraffic:Exasol:Fingerprint", configuration["EXASOL_FINGERPRINT"]);
    AddIfPresent(overrides, "SpaceTraffic:Exasol:Schema", configuration["EXASOL_SCHEMA"]);
    AddIfPresent(overrides, "SpaceTraffic:Exasol:Username", configuration["EXASOL_USER"]);
    AddIfPresent(overrides, "SpaceTraffic:Exasol:Password", configuration["EXASOL_PASSWORD"]);
    AddIfPresent(overrides, "SpaceTraffic:Exasol:OdbcDriver", configuration["EXASOL_ODBC_DRIVER"]);

    if (double.TryParse(configuration["ETL_INTERVAL_HOURS"], out var intervalHours) && intervalHours > 0)
    {
        overrides["SpaceTraffic:Etl:Interval"] = TimeSpan.FromHours(intervalHours).ToString();
    }

    return overrides;
}

static void AddIfPresent(IDictionary<string, string?> values, string key, string? value)
{
    if (!string.IsNullOrWhiteSpace(value))
    {
        values[key] = value;
    }
}
