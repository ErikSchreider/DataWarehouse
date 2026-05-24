using Microsoft.Extensions.Options;
using SpaceTrafficETL.Configuration;
using SpaceTrafficETL.Export;
using SpaceTrafficETL.Parsers;
using SpaceTrafficETL.Services;
using SpaceTrafficETL.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<SpaceTrafficOptions>()
    .Bind(builder.Configuration.GetSection(SpaceTrafficOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => options.Etl.Interval > TimeSpan.Zero, "ETL interval must be greater than zero.")
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
builder.Services.AddSingleton<ITleParserService, TleParserService>();
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
builder.Services.AddSingleton<ISpaceTrafficEtlPipeline, SpaceTrafficEtlPipeline>();
builder.Services.AddHostedService<SpaceTrafficWorker>();

await builder.Build().RunAsync();
