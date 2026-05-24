using SpaceTrafficETL.Configuration;

namespace SpaceTrafficETL.Models;

public sealed record RawDataset(
    string SourceName,
    DataSourceKind Kind,
    Uri SourceUri,
    DateTimeOffset DownloadedAt,
    string ContentType,
    string RawFilePath);
