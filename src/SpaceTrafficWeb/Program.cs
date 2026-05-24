using SpaceTrafficWeb.Configuration;
using SpaceTrafficWeb.Components;
using SpaceTrafficWeb.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddInMemoryCollection(BuildEnvironmentOverrides(builder.Configuration));

builder.Services
    .AddOptions<ExasolOptions>()
    .Bind(builder.Configuration.GetSection(ExasolOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(options => !string.IsNullOrWhiteSpace(options.Host), "EXASOL_HOST is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Schema), "EXASOL_SCHEMA is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.Username), "EXASOL_USER is required.")
    .ValidateOnStart();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<IExasolQueryService, ExasolQueryService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();

static Dictionary<string, string?> BuildEnvironmentOverrides(IConfiguration configuration)
{
    var overrides = new Dictionary<string, string?>();

    AddIfPresent(overrides, "Exasol:Host", configuration["EXASOL_HOST"]);
    AddIfPresent(overrides, "Exasol:Port", configuration["EXASOL_PORT"]);
    AddIfPresent(overrides, "Exasol:Schema", configuration["EXASOL_SCHEMA"]);
    AddIfPresent(overrides, "Exasol:Username", configuration["EXASOL_USER"]);
    AddIfPresent(overrides, "Exasol:Password", configuration["EXASOL_PASSWORD"]);

    return overrides;
}

static void AddIfPresent(IDictionary<string, string?> values, string key, string? value)
{
    if (!string.IsNullOrWhiteSpace(value))
    {
        values[key] = value;
    }
}
