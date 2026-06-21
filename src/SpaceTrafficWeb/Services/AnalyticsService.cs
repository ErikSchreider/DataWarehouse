using System.Data.Common;
using SpaceTrafficWeb.Models;

namespace SpaceTrafficWeb.Services;

public sealed class AnalyticsService(IExasolQueryService queryService) : IAnalyticsService
{
    public async Task<AnalyticsDashboard> GetAnalyticsAsync(CancellationToken cancellationToken)
    {
        var orbitRollupTask = GetOrbitRollupAsync(cancellationToken);
        var dailyTrendTask = GetDailyTrendAsync(cancellationToken);
        var operatorRankingTask = GetOperatorRankingAsync(cancellationToken);
        var orbitStatisticsTask = GetOrbitStatisticsAsync(cancellationToken);
        var skylineRisksTask = GetSkylineRisksAsync(cancellationToken);
        var purposeDistributionTask = GetPurposeDistributionAsync(cancellationToken);
        var sourceCoverageTask = GetSourceCoverageAsync(cancellationToken);

        await Task.WhenAll(
            orbitRollupTask,
            dailyTrendTask,
            operatorRankingTask,
            orbitStatisticsTask,
            skylineRisksTask,
            purposeDistributionTask,
            sourceCoverageTask);

        return new AnalyticsDashboard(
            await orbitRollupTask,
            await dailyTrendTask,
            await operatorRankingTask,
            await orbitStatisticsTask,
            await skylineRisksTask,
            await purposeDistributionTask,
            await sourceCoverageTask);
    }

    private Task<IReadOnlyList<OrbitRollupRow>> GetOrbitRollupAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COALESCE(b."orbit_class", 'ALL_ORBITS') AS orbit_class,
                COALESCE(o."object_type", 'ALL_OBJECT_TYPES') AS object_type,
                SUM(f."object_count") AS object_count,
                AVG(f."debris_risk_score") AS average_risk_score
            FROM ${SCHEMA}."fact_object_position_observation" f
            LEFT JOIN ${SCHEMA}."dim_orbit_band" b
                ON f."orbit_band_id" = b."orbit_band_id"
            LEFT JOIN ${SCHEMA}."dim_space_object" o
                ON f."object_id" = o."object_id"
            GROUP BY GROUPING SETS
            (
                (b."orbit_class", o."object_type"),
                (b."orbit_class"),
                ()
            )
            ORDER BY object_count DESC
            LIMIT 12
            """;

        return queryService.QueryAsync(sql, reader => new OrbitRollupRow(
            GetString(reader, "orbit_class"),
            GetString(reader, "object_type"),
            GetInt64(reader, "object_count"),
            GetNullableDouble(reader, "average_risk_score")), cancellationToken);
    }

    private Task<IReadOnlyList<DailyTrendRow>> GetDailyTrendAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            WITH daily_counts AS
            (
                SELECT
                    CAST(f."observation_timestamp" AS DATE) AS observation_date,
                    SUM(f."object_count") AS object_count
                FROM ${SCHEMA}."fact_object_position_observation" f
                WHERE f."observation_timestamp" IS NOT NULL
                GROUP BY CAST(f."observation_timestamp" AS DATE)
            )
            SELECT
                observation_date,
                object_count,
                AVG(object_count) OVER
                (
                    ORDER BY observation_date
                    ROWS BETWEEN 6 PRECEDING AND CURRENT ROW
                ) AS moving_average_object_count
            FROM daily_counts
            ORDER BY observation_date DESC
            LIMIT 30
            """;

        return queryService.QueryAsync(sql, reader => new DailyTrendRow(
            GetDateTime(reader, "observation_date"),
            GetInt64(reader, "object_count"),
            GetNullableDouble(reader, "moving_average_object_count")), cancellationToken);
    }

    private Task<IReadOnlyList<OperatorRankingRow>> GetOperatorRankingAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            WITH operator_counts AS
            (
                SELECT
                    COALESCE(op."operator_name", 'UNKNOWN') AS operator_name,
                    COALESCE(op."country", 'UNKNOWN') AS country,
                    SUM(f."object_count") AS object_count,
                    AVG(f."debris_risk_score") AS average_risk_score
                FROM ${SCHEMA}."fact_object_position_observation" f
                JOIN ${SCHEMA}."dim_space_object" o
                    ON f."object_id" = o."object_id"
                LEFT JOIN ${SCHEMA}."dim_operator" op
                    ON o."operator_id" = op."operator_id"
                GROUP BY
                    COALESCE(op."operator_name", 'UNKNOWN'),
                    COALESCE(op."country", 'UNKNOWN')
            )
            SELECT
                operator_name,
                country,
                object_count,
                average_risk_score,
                DENSE_RANK() OVER (ORDER BY object_count DESC) AS operator_rank
            FROM operator_counts
            ORDER BY operator_rank
            LIMIT 10
            """;

        return queryService.QueryAsync(sql, reader => new OperatorRankingRow(
            GetString(reader, "operator_name"),
            GetString(reader, "country"),
            GetInt64(reader, "object_count"),
            GetNullableDouble(reader, "average_risk_score"),
            GetInt64(reader, "operator_rank")), cancellationToken);
    }

    private Task<IReadOnlyList<OrbitStatisticRow>> GetOrbitStatisticsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COALESCE(b."orbit_class", 'UNKNOWN') AS orbit_class,
                SUM(f."object_count") AS object_count,
                AVG(f."altitude_km") AS average_altitude_km,
                STDDEV_POP(f."altitude_km") AS altitude_stddev_km,
                AVG(f."debris_risk_score") AS average_risk_score
            FROM ${SCHEMA}."fact_object_position_observation" f
            LEFT JOIN ${SCHEMA}."dim_orbit_band" b
                ON f."orbit_band_id" = b."orbit_band_id"
            GROUP BY COALESCE(b."orbit_class", 'UNKNOWN')
            ORDER BY object_count DESC
            """;

        return queryService.QueryAsync(sql, reader => new OrbitStatisticRow(
            GetString(reader, "orbit_class"),
            GetInt64(reader, "object_count"),
            GetNullableDouble(reader, "average_altitude_km"),
            GetNullableDouble(reader, "altitude_stddev_km"),
            GetNullableDouble(reader, "average_risk_score")), cancellationToken);
    }

    private Task<IReadOnlyList<SkylineRiskRow>> GetSkylineRisksAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            WITH candidates AS
            (
                SELECT
                    COALESCE(b."orbit_class", 'UNKNOWN') AS orbit_class,
                    COALESCE(op."operator_name", 'UNKNOWN') AS operator_name,
                    SUM(f."object_count") AS object_count,
                    COALESCE(AVG(f."debris_risk_score"), 0) AS average_risk_score
                FROM ${SCHEMA}."fact_object_position_observation" f
                LEFT JOIN ${SCHEMA}."dim_orbit_band" b
                    ON f."orbit_band_id" = b."orbit_band_id"
                JOIN ${SCHEMA}."dim_space_object" o
                    ON f."object_id" = o."object_id"
                LEFT JOIN ${SCHEMA}."dim_operator" op
                    ON o."operator_id" = op."operator_id"
                GROUP BY
                    COALESCE(b."orbit_class", 'UNKNOWN'),
                    COALESCE(op."operator_name", 'UNKNOWN')
            )
            SELECT
                c.orbit_class,
                c.operator_name,
                c.object_count,
                c.average_risk_score
            FROM candidates c
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM candidates other
                WHERE other.object_count >= c.object_count
                  AND other.average_risk_score >= c.average_risk_score
                  AND (
                      other.object_count > c.object_count
                      OR other.average_risk_score > c.average_risk_score
                  )
            )
            ORDER BY c.average_risk_score DESC, c.object_count DESC
            LIMIT 10
            """;

        return queryService.QueryAsync(sql, reader => new SkylineRiskRow(
            GetString(reader, "orbit_class"),
            GetString(reader, "operator_name"),
            GetInt64(reader, "object_count"),
            GetNullableDouble(reader, "average_risk_score")), cancellationToken);
    }

    private Task<IReadOnlyList<PurposeDistributionRow>> GetPurposeDistributionAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COALESCE(o."purpose", 'UNKNOWN') AS purpose,
                SUM(f."object_count") AS object_count
            FROM ${SCHEMA}."fact_object_position_observation" f
            JOIN ${SCHEMA}."dim_space_object" o
                ON f."object_id" = o."object_id"
            GROUP BY COALESCE(o."purpose", 'UNKNOWN')
            ORDER BY object_count DESC
            LIMIT 10
            """;

        return queryService.QueryAsync(sql, reader => new PurposeDistributionRow(
            GetString(reader, "purpose"),
            GetInt64(reader, "object_count")), cancellationToken);
    }

    private Task<IReadOnlyList<SourceCoverageRow>> GetSourceCoverageAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                s."source_name",
                COUNT(f."observation_id") AS observation_count,
                MAX(f."observation_timestamp") AS last_observation_timestamp
            FROM ${SCHEMA}."dim_source" s
            LEFT JOIN ${SCHEMA}."fact_object_position_observation" f
                ON s."source_id" = f."source_id"
            GROUP BY s."source_name"
            ORDER BY observation_count DESC
            """;

        return queryService.QueryAsync(sql, reader => new SourceCoverageRow(
            GetString(reader, "source_name"),
            GetInt64(reader, "observation_count"),
            GetNullableDateTime(reader, "last_observation_timestamp")), cancellationToken);
    }

    private static string GetString(DbDataReader reader, string name)
    {
        var value = reader[name];
        return value == DBNull.Value ? string.Empty : Convert.ToString(value) ?? string.Empty;
    }

    private static long GetInt64(DbDataReader reader, string name)
    {
        var value = reader[name];
        return value == DBNull.Value ? 0 : Convert.ToInt64(value);
    }

    private static double? GetNullableDouble(DbDataReader reader, string name)
    {
        var value = reader[name];
        return value == DBNull.Value ? null : Convert.ToDouble(value);
    }

    private static DateTime GetDateTime(DbDataReader reader, string name)
    {
        return Convert.ToDateTime(reader[name]);
    }

    private static DateTime? GetNullableDateTime(DbDataReader reader, string name)
    {
        var value = reader[name];
        return value == DBNull.Value ? null : Convert.ToDateTime(value);
    }
}
