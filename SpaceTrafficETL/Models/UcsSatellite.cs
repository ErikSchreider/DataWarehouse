namespace SpaceTrafficETL.Models;

public sealed record UcsSatellite(
    string Name,
    int? NoradId,
    string? Operator,
    string? Country,
    string? Purpose,
    string? OrbitClass,
    DateOnly? LaunchDate,
    string SourceName,
    DateTimeOffset DownloadedAt,
    string RawFilePath);
