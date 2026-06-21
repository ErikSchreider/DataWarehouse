using System.ComponentModel.DataAnnotations;

namespace SpaceTrafficETL.Configuration;

public sealed class SpaceTrafficOptions
{
    public const string SectionName = "SpaceTraffic";

    [Required]
    public string UserAgent { get; init; } = "SpaceTrafficETL/1.0";

    [Required]
    public ExasolOptions Exasol { get; set; } = new();

    [Required]
    public DataDirectoryOptions DataDirectories { get; set; } = new();

    [Required]
    public EtlOptions Etl { get; set; } = new();

    [Required]
    public CelesTrakOptions CelesTrak { get; init; } = new();

    [Required]
    public UcsOptions Ucs { get; init; } = new();

    [Required]
    public LaunchDataOptions LaunchData { get; init; } = new();

    public CsvOptions Csv { get; init; } = new();
}

public sealed class ExasolOptions
{
    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 8563;

    public string Database { get; set; } = string.Empty;

    [Required]
    public string Schema { get; set; } = "SPACE_TRAFFIC";

    [Required]
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Fingerprint { get; set; } = string.Empty;

    [Required]
    public string OdbcDriver { get; set; } = "EXASOL";

    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

public sealed class DataDirectoryOptions
{
    [Required]
    public string StorageRoot { get; init; } = "data";

    [Required]
    public string Raw { get; init; } = "raw";

    [Required]
    public string Staging { get; init; } = "staging";

    [Required]
    public string Processed { get; init; } = "processed";

    [Required]
    public string Rejected { get; init; } = "rejected";
}

public sealed class EtlOptions
{
    public bool RunOnStartup { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(24);

    public TimeSpan DailyRunTimeUtc { get; init; } = new(2, 0, 0);

    public TimeSpan DownloadTimeout { get; init; } = TimeSpan.FromMinutes(2);

    [Range(1, 10)]
    public int MaxDownloadRetries { get; init; } = 3;

    public TimeSpan DownloadRetryDelay { get; init; } = TimeSpan.FromSeconds(5);
}

public sealed class CelesTrakOptions
{
    public bool Enabled { get; init; } = true;

    [Required]
    public DataSourceOptions[] Sources { get; init; } =
    [
        new()
        {
            Name = "celestrak-active",
            Kind = DataSourceKind.CelesTrakJson,
            Url = new("https://celestrak.org/NORAD/elements/gp.php?GROUP=active&FORMAT=json"),
            RawFileExtension = ".json"
        },
        new()
        {
            Name = "celestrak-debris",
            Kind = DataSourceKind.CelesTrakJson,
            Url = new("https://celestrak.org/NORAD/elements/gp.php?GROUP=debris&FORMAT=json"),
            RawFileExtension = ".json"
        },
        new()
        {
            Name = "celestrak-starlink",
            Kind = DataSourceKind.CelesTrakJson,
            Url = new("https://celestrak.org/NORAD/elements/gp.php?GROUP=starlink&FORMAT=json"),
            RawFileExtension = ".json"
        },
        new()
        {
            Name = "celestrak-geo",
            Kind = DataSourceKind.CelesTrakJson,
            Url = new("https://celestrak.org/NORAD/elements/gp.php?GROUP=geo&FORMAT=json"),
            RawFileExtension = ".json"
        },
        new()
        {
            Name = "celestrak-stations",
            Kind = DataSourceKind.CelesTrakJson,
            Url = new("https://celestrak.org/NORAD/elements/gp.php?GROUP=stations&FORMAT=json"),
            RawFileExtension = ".json"
        },
        new()
        {
            Name = "celestrak-last-30-days",
            Kind = DataSourceKind.CelesTrakJson,
            Url = new("https://celestrak.org/NORAD/elements/gp.php?GROUP=last-30-days&FORMAT=json"),
            RawFileExtension = ".json"
        }
    ];

    [Required]
    public string RawFileExtension { get; init; } = ".json";
}

public sealed class UcsOptions
{
    public bool Enabled { get; init; } = true;

    [Required]
    public Uri SatelliteDatabasePageUrl { get; init; } = new("https://www.ucsusa.org/resources/satellite-database");

    [Required]
    public Uri SatelliteDatabaseDownloadUrl { get; init; } = new("https://www.ucsusa.org/resources/satellite-database");

    [Required]
    public string RawFileExtension { get; init; } = ".html";
}

public sealed class LaunchDataOptions
{
    public bool Enabled { get; init; } = true;

    [Required]
    public Uri LaunchesUrl { get; init; } = new("https://ll.thespacedevs.com/2.2.0/launch/");

    [Required]
    public string RawFileExtension { get; init; } = ".json";
}

public sealed class DataSourceOptions
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public DataSourceKind Kind { get; init; }

    [Required]
    public Uri Url { get; init; } = new("about:blank");

    public bool Enabled { get; init; } = true;

    [Required]
    public string RawFileExtension { get; init; } = ".dat";
}

public sealed class CsvOptions
{
    [Required]
    public string OutputDirectory { get; init; } = "staging";

    [Required]
    public string Delimiter { get; init; } = ",";

    public bool IncludeHeader { get; init; } = true;
}

public enum DataSourceKind
{
    Tle,
    CelesTrakJson,
    UcsSatelliteDatabase,
    SpaceDevsLaunches
}
