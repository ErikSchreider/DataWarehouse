namespace SpaceTrafficWeb.Models;

public sealed record OrbitRollupRow(
    string OrbitClass,
    string ObjectType,
    long ObjectCount,
    double? AverageRiskScore);

public sealed record DailyTrendRow(
    DateTime ObservationDate,
    long ObjectCount,
    double? MovingAverageObjectCount);

public sealed record OperatorRankingRow(
    string OperatorName,
    string Country,
    long ObjectCount,
    double? AverageRiskScore,
    long Rank);

public sealed record OrbitStatisticRow(
    string OrbitClass,
    long ObjectCount,
    double? AverageAltitudeKm,
    double? AltitudeStdDevKm,
    double? AverageRiskScore);

public sealed record SkylineRiskRow(
    string OrbitClass,
    string OperatorName,
    long ObjectCount,
    double? AverageRiskScore);

public sealed record PurposeDistributionRow(
    string Purpose,
    long ObjectCount);

public sealed record SourceCoverageRow(
    string SourceName,
    long ObservationCount,
    DateTime? LastObservationTimestamp);

public sealed record AnalyticsDashboard(
    IReadOnlyList<OrbitRollupRow> OrbitRollup,
    IReadOnlyList<DailyTrendRow> DailyTrend,
    IReadOnlyList<OperatorRankingRow> OperatorRanking,
    IReadOnlyList<OrbitStatisticRow> OrbitStatistics,
    IReadOnlyList<SkylineRiskRow> SkylineRisks,
    IReadOnlyList<PurposeDistributionRow> PurposeDistribution,
    IReadOnlyList<SourceCoverageRow> SourceCoverage);
