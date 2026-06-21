namespace SpaceTrafficETL.Models;

public sealed record LaunchRecord(
    string LaunchId,
    string LaunchName,
    DateTimeOffset? LaunchDate,
    string? LaunchProvider,
    string? RocketName,
    string? LaunchCountry,
    string? LaunchStatus,
    int? PayloadCount,
    string SourceName,
    DateTimeOffset DownloadedAt,
    string RawFilePath);
