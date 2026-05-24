namespace SpaceTrafficETL.Models;

public sealed record StagingSatelliteRecord(
    int NoradId,
    string SatelliteName,
    string Classification,
    string InternationalDesignator,
    int EpochYear,
    double EpochDay,
    string SourceName,
    DateTimeOffset DownloadedAt,
    string RawFilePath);
