using System.Data.Common;
using SpaceTrafficWeb.Models;

namespace SpaceTrafficWeb.Services;

public sealed class DashboardService(IExasolQueryService queryService) : IDashboardService
{
    public async Task<DashboardSummary> GetDashboardSummaryAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COUNT(*) AS total_objects,
                SUM(CASE
                    WHEN UPPER(COALESCE("operational_status", '')) LIKE '%ACTIVE%' THEN 1
                    WHEN UPPER(COALESCE("operational_status", '')) LIKE '%OPERATIONAL%' THEN 1
                    ELSE 0
                END) AS active_satellites,
                SUM(CASE WHEN UPPER(COALESCE("object_type", '')) LIKE '%DEBRIS%' THEN 1 ELSE 0 END) AS debris_objects,
                (
                    SELECT MAX("imported_at")
                    FROM ${SCHEMA}."stg_celestrak_objects"
                ) AS last_import_timestamp
            FROM ${SCHEMA}."dim_space_object"
            """;

        return await queryService.QuerySingleAsync(sql, reader => new DashboardSummary(
                GetInt64(reader, "total_objects"),
                GetInt64(reader, "active_satellites"),
                GetInt64(reader, "debris_objects"),
                GetNullableDateTime(reader, "last_import_timestamp")),
            cancellationToken)
            ?? new DashboardSummary(0, 0, 0, null);
    }

    public Task<IReadOnlyList<OrbitClassCount>> GetObjectsByOrbitClassAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COALESCE(b."orbit_class", 'UNKNOWN') AS orbit_class,
                COUNT(*) AS object_count
            FROM ${SCHEMA}."fact_object_position_observation" f
            LEFT JOIN ${SCHEMA}."dim_orbit_band" b
                ON f."orbit_band_id" = b."orbit_band_id"
            GROUP BY COALESCE(b."orbit_class", 'UNKNOWN')
            ORDER BY object_count DESC
            """;

        return queryService.QueryAsync(sql, reader => new OrbitClassCount(
            GetString(reader, "orbit_class"),
            GetInt64(reader, "object_count")), cancellationToken);
    }

    public Task<IReadOnlyList<ObjectTypeAltitude>> GetAverageAltitudeByObjectTypeAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COALESCE(o."object_type", 'UNKNOWN') AS object_type,
                AVG(f."altitude_km") AS average_altitude_km
            FROM ${SCHEMA}."fact_object_position_observation" f
            JOIN ${SCHEMA}."dim_space_object" o
                ON f."object_id" = o."object_id"
            GROUP BY COALESCE(o."object_type", 'UNKNOWN')
            ORDER BY object_type
            """;

        return queryService.QueryAsync(sql, reader => new ObjectTypeAltitude(
            GetString(reader, "object_type"),
            GetNullableDouble(reader, "average_altitude_km")), cancellationToken);
    }

    public Task<IReadOnlyList<SourceSummary>> GetSourcesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                s."source_id",
                s."source_name",
                s."source_type",
                s."source_url",
                MAX(f."observation_timestamp") AS last_import_timestamp
            FROM ${SCHEMA}."dim_source" s
            LEFT JOIN ${SCHEMA}."fact_object_position_observation" f
                ON s."source_id" = f."source_id"
            GROUP BY
                s."source_id",
                s."source_name",
                s."source_type",
                s."source_url"
            ORDER BY s."source_id"
            """;

        return queryService.QueryAsync(sql, reader => new SourceSummary(
            GetInt64(reader, "source_id"),
            GetString(reader, "source_name"),
            GetString(reader, "source_type"),
            GetString(reader, "source_url"),
            GetNullableDateTime(reader, "last_import_timestamp")), cancellationToken);
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

    private static DateTime? GetNullableDateTime(DbDataReader reader, string name)
    {
        var value = reader[name];
        return value == DBNull.Value ? null : Convert.ToDateTime(value);
    }
}
