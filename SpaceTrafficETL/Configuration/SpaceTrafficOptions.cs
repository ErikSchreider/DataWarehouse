using System.ComponentModel.DataAnnotations;

namespace SpaceTrafficETL.Configuration;

public sealed class SpaceTrafficOptions
{
    public const string SectionName = "SpaceTraffic";

    [Required]
    public string UserAgent { get; init; } = "SpaceTrafficETL/1.0";

    [Required]
    public ExasolOptions Exasol { get; init; } = new();

    [Required]
    public DataDirectoryOptions DataDirectories { get; init; } = new();

    [Required]
    public EtlOptions Etl { get; init; } = new();

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
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 8563;

    [Required]
    public string Database { get; init; } = string.Empty;

    [Required]
    public string Schema { get; init; } = "STAGING";

    [Required]
    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    [Required]
    public string ImportTable { get; init; } = "SATELLITE_TLE_STAGE";

    public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromSeconds(30);
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
    public bool RunOnStartup { get; init; } = true;

    public TimeSpan Interval { get; init; } = TimeSpan.FromHours(24);

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
    public Uri ActiveSatellitesTleUrl { get; init; } = new("https://celestrak.org/NORAD/elements/gp.php?GROUP=active&FORMAT=tle");

    [Required]
    public Uri StationsTleUrl { get; init; } = new("https://celestrak.org/NORAD/elements/gp.php?GROUP=stations&FORMAT=tle");

    [Required]
    public Uri GpQueryBaseUrl { get; init; } = new("https://celestrak.org/NORAD/elements/gp.php");

    [Required]
    public string RawFileExtension { get; init; } = ".tle";
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
    public bool Enabled { get; init; }

    [Required]
    public Uri UpcomingLaunchesUrl { get; init; } = new("https://ll.thespacedevs.com/2.3.0/launches/upcoming/");

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
    UcsSatelliteDatabase,
    SpaceDevsLaunches
}
