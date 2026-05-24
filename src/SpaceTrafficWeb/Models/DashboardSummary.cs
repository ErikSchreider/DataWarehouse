namespace SpaceTrafficWeb.Models;

public sealed record DashboardSummary(
    long TotalObjects,
    long ActiveSatellites,
    long DebrisObjects,
    DateTime? LastImportTimestamp);
