OPEN SCHEMA ${SCHEMA};

MERGE INTO "dim_source" target
USING (
    SELECT 1 AS "source_id", 'CelesTrak' AS "source_name", 'TLE' AS "source_type", 'https://celestrak.org' AS "source_url"
    UNION ALL
    SELECT 2 AS "source_id", 'UCS Satellite Database' AS "source_name", 'SATELLITE_DATABASE' AS "source_type", 'https://www.ucsusa.org/resources/satellite-database' AS "source_url"
    UNION ALL
    SELECT 3 AS "source_id", 'Launch Data API' AS "source_name", 'LAUNCH_API' AS "source_type", 'https://ll.thespacedevs.com' AS "source_url"
) source
ON target."source_id" = source."source_id"
WHEN MATCHED THEN UPDATE SET
    target."source_name" = source."source_name",
    target."source_type" = source."source_type",
    target."source_url" = source."source_url"
WHEN NOT MATCHED THEN INSERT
(
    "source_id",
    "source_name",
    "source_type",
    "source_url"
)
VALUES
(
    source."source_id",
    source."source_name",
    source."source_type",
    source."source_url"
);

MERGE INTO "dim_operator" target
USING (
    SELECT
        COALESCE(existing."operator_id", max_ids."max_operator_id" + missing."new_operator_seq") AS "operator_id",
        source."operator_name",
        source."operator_type",
        source."country"
    FROM (
        SELECT
            COALESCE("operator_name", 'UNKNOWN') AS "operator_name",
            'UNKNOWN' AS "operator_type",
            MAX("country") AS "country"
        FROM "stg_ucs_satellites"
        GROUP BY COALESCE("operator_name", 'UNKNOWN')
    ) source
    LEFT JOIN "dim_operator" existing
        ON source."operator_name" = existing."operator_name"
    LEFT JOIN (
        SELECT
            "operator_name",
            ROW_NUMBER() OVER (ORDER BY "operator_name") AS "new_operator_seq"
        FROM (
            SELECT DISTINCT COALESCE(stg."operator_name", 'UNKNOWN') AS "operator_name"
            FROM "stg_ucs_satellites" stg
            WHERE NOT EXISTS (
                SELECT 1
                FROM "dim_operator" dim
                WHERE dim."operator_name" = COALESCE(stg."operator_name", 'UNKNOWN')
            )
        )
    ) missing
        ON source."operator_name" = missing."operator_name"
    CROSS JOIN (
        SELECT COALESCE(MAX("operator_id"), 0) AS "max_operator_id"
        FROM "dim_operator"
    ) max_ids
) source
ON target."operator_name" = source."operator_name"
WHEN MATCHED THEN UPDATE SET
    target."operator_type" = source."operator_type",
    target."country" = source."country"
WHEN NOT MATCHED THEN INSERT
(
    "operator_id",
    "operator_name",
    "operator_type",
    "country"
)
VALUES
(
    source."operator_id",
    source."operator_name",
    source."operator_type",
    source."country"
);

MERGE INTO "dim_orbit_band" target
USING (
    SELECT 1 AS "orbit_band_id", 'LEO' AS "orbit_class", 160 AS "altitude_min_km", 2000 AS "altitude_max_km", 'LOW_INCLINATION' AS "inclination_class"
    UNION ALL
    SELECT 2 AS "orbit_band_id", 'MEO' AS "orbit_class", 2000 AS "altitude_min_km", 35786 AS "altitude_max_km", 'MEDIUM_INCLINATION' AS "inclination_class"
    UNION ALL
    SELECT 3 AS "orbit_band_id", 'GEO' AS "orbit_class", 35786 AS "altitude_min_km", 35786 AS "altitude_max_km", 'EQUATORIAL' AS "inclination_class"
    UNION ALL
    SELECT 4 AS "orbit_band_id", 'HEO' AS "orbit_class", 35786 AS "altitude_min_km", NULL AS "altitude_max_km", 'HIGH_INCLINATION' AS "inclination_class"
) source
ON target."orbit_band_id" = source."orbit_band_id"
WHEN MATCHED THEN UPDATE SET
    target."orbit_class" = source."orbit_class",
    target."altitude_min_km" = source."altitude_min_km",
    target."altitude_max_km" = source."altitude_max_km",
    target."inclination_class" = source."inclination_class"
WHEN NOT MATCHED THEN INSERT
(
    "orbit_band_id",
    "orbit_class",
    "altitude_min_km",
    "altitude_max_km",
    "inclination_class"
)
VALUES
(
    source."orbit_band_id",
    source."orbit_class",
    source."altitude_min_km",
    source."altitude_max_km",
    source."inclination_class"
);

MERGE INTO "dim_space_object" target
USING (
    SELECT
        c."norad_id" AS "object_id",
        c."norad_id",
        MAX(c."object_name") AS "object_name",
        'SATELLITE' AS "object_type",
        MAX(u."operational_status") AS "operational_status",
        MAX(u."purpose") AS "purpose",
        MAX(u."launch_date") AS "launch_date",
        MAX(o."operator_id") AS "operator_id"
    FROM "stg_celestrak_objects" c
    LEFT JOIN "stg_ucs_satellites" u
        ON c."norad_id" = u."norad_id"
    LEFT JOIN "dim_operator" o
        ON COALESCE(u."operator_name", 'UNKNOWN') = o."operator_name"
    WHERE c."norad_id" IS NOT NULL
    GROUP BY c."norad_id"
) source
ON target."norad_id" = source."norad_id"
WHEN MATCHED THEN UPDATE SET
    target."object_name" = source."object_name",
    target."object_type" = source."object_type",
    target."operational_status" = source."operational_status",
    target."purpose" = source."purpose",
    target."launch_date" = source."launch_date",
    target."operator_id" = source."operator_id"
WHEN NOT MATCHED THEN INSERT
(
    "object_id",
    "norad_id",
    "object_name",
    "object_type",
    "operational_status",
    "purpose",
    "launch_date",
    "operator_id"
)
VALUES
(
    source."object_id",
    source."norad_id",
    source."object_name",
    source."object_type",
    source."operational_status",
    source."purpose",
    source."launch_date",
    source."operator_id"
);
