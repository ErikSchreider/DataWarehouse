namespace SpaceTrafficETL.Models;

public sealed record CelesTrakObject(
    string ObjectName,
    int NoradId,
    DateTimeOffset Epoch,
    double InclinationDegrees,
    double Eccentricity,
    double MeanMotionRevolutionsPerDay,
    string SourceGroup,
    string SourceName,
    DateTimeOffset DownloadedAt,
    string RawFilePath);
