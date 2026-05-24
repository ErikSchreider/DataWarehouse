namespace SpaceTrafficWeb.Models;

public sealed record SourceSummary(
    long SourceId,
    string SourceName,
    string SourceType,
    string SourceUrl,
    DateTime? LastImportTimestamp);
