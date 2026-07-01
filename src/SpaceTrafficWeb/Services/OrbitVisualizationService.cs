using System.Data.Common;
using SpaceTrafficWeb.Models;

namespace SpaceTrafficWeb.Services;

public sealed class OrbitVisualizationService(IExasolQueryService queryService) : IOrbitVisualizationService
{
    public Task<IReadOnlyList<OrbitObjectPoint>> GetOrbitObjectsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            WITH latest_observations AS
            (
                SELECT
                    f."object_id",
                    f."orbit_band_id",
                    f."source_id",
                    f."observation_timestamp",
                    f."altitude_km",
                    f."velocity_km_s",
                    f."inclination_deg",
                    f."debris_risk_score",
                    ROW_NUMBER() OVER
                    (
                        PARTITION BY f."object_id"
                        ORDER BY
                            CASE WHEN f."source_id" = 900 THEN 1 ELSE 0 END,
                            f."observation_timestamp" DESC
                    ) AS "rn"
                FROM ${SCHEMA}."fact_object_position_observation" f
            )
            SELECT
                o."object_id",
                o."norad_id",
                COALESCE(o."object_name", 'UNKNOWN') AS object_name,
                COALESCE(o."object_type", 'UNKNOWN') AS object_type,
                COALESCE(b."orbit_class", 'UNKNOWN') AS orbit_class,
                COALESCE(
                    lo."altitude_km",
                    CASE
                        WHEN b."orbit_class" = 'LEO' THEN 550
                        WHEN b."orbit_class" = 'MEO' THEN 20200
                        WHEN b."orbit_class" = 'GEO' THEN 35786
                        WHEN b."orbit_class" = 'HEO' THEN 42000
                        ELSE NULL
                    END
                ) AS altitude_km,
                COALESCE(
                    lo."velocity_km_s",
                    CASE
                        WHEN b."orbit_class" = 'LEO' THEN 7.8
                        WHEN b."orbit_class" = 'MEO' THEN 3.9
                        WHEN b."orbit_class" = 'GEO' THEN 3.1
                        WHEN b."orbit_class" = 'HEO' THEN 2.5
                        ELSE NULL
                    END
                ) AS velocity_km_s,
                lo."inclination_deg",
                lo."debris_risk_score",
                COALESCE(s."source_name", 'UNKNOWN') AS source_name,
                COALESCE(op."operator_name", 'UNKNOWN') AS operator_name,
                COALESCE(o."purpose", 'UNKNOWN') AS purpose,
                lo."observation_timestamp"
            FROM latest_observations lo
            JOIN ${SCHEMA}."dim_space_object" o
                ON lo."object_id" = o."object_id"
            LEFT JOIN ${SCHEMA}."dim_orbit_band" b
                ON lo."orbit_band_id" = b."orbit_band_id"
            LEFT JOIN ${SCHEMA}."dim_source" s
                ON lo."source_id" = s."source_id"
            LEFT JOIN ${SCHEMA}."dim_operator" op
                ON o."operator_id" = op."operator_id"
            WHERE lo."rn" = 1
            ORDER BY o."object_id"
            """;

        return queryService.QueryAsync(sql, reader => new OrbitObjectPoint(
            GetInt64(reader, "object_id"),
            GetNullableInt64(reader, "norad_id"),
            GetString(reader, "object_name"),
            GetString(reader, "object_type"),
            GetString(reader, "orbit_class"),
            GetNullableDouble(reader, "altitude_km"),
            GetNullableDouble(reader, "velocity_km_s"),
            GetNullableDouble(reader, "inclination_deg"),
            GetNullableDouble(reader, "debris_risk_score"),
            GetString(reader, "source_name"),
            GetString(reader, "operator_name"),
            GetString(reader, "purpose"),
            GetNullableDateTime(reader, "observation_timestamp")), cancellationToken);
    }

    private static string GetString(DbDataReader reader, string name)
    {
        var value = reader[name];
        return value == DBNull.Value ? string.Empty : Convert.ToString(value) ?? string.Empty;
    }

    private static long GetInt64(DbDataReader reader, string name)
    {
        return Convert.ToInt64(reader[name]);
    }

    private static long? GetNullableInt64(DbDataReader reader, string name)
    {
        var value = reader[name];
        return value == DBNull.Value ? null : Convert.ToInt64(value);
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
