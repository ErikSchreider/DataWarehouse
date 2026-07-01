OPEN SCHEMA ${SCHEMA};

DELETE FROM "dim_space_object";
DELETE FROM "dim_operator";
DELETE FROM "dim_orbit_band";
DELETE FROM "dim_source";

INSERT INTO "dim_source"
(
    "source_id",
    "source_name",
    "source_type",
    "source_url"
)
SELECT 1, 'CelesTrak active', 'CELESTRAK_GP_JSON', 'https://celestrak.org/NORAD/elements/gp.php?GROUP=active&FORMAT=json'
UNION ALL
SELECT 2, 'CelesTrak debris', 'CELESTRAK_GP_JSON', 'https://celestrak.org/NORAD/elements/index.php?FORMAT=json'
UNION ALL
SELECT 3, 'CelesTrak starlink', 'CELESTRAK_GP_JSON', 'https://celestrak.org/NORAD/elements/gp.php?GROUP=starlink&FORMAT=json'
UNION ALL
SELECT 4, 'CelesTrak geo', 'CELESTRAK_GP_JSON', 'https://celestrak.org/NORAD/elements/gp.php?GROUP=geo&FORMAT=json'
UNION ALL
SELECT 5, 'CelesTrak stations', 'CELESTRAK_GP_JSON', 'https://celestrak.org/NORAD/elements/gp.php?GROUP=stations&FORMAT=json'
UNION ALL
SELECT 6, 'CelesTrak last-30-days', 'CELESTRAK_GP_JSON', 'https://celestrak.org/NORAD/elements/gp.php?GROUP=last-30-days&FORMAT=json'
UNION ALL
SELECT 100, 'UCS Satellite Database', 'SATELLITE_DATABASE', 'https://www.ucsusa.org/resources/satellite-database'
UNION ALL
SELECT 200, 'SpaceDevs Launch Library API', 'LAUNCH_API', 'https://ll.thespacedevs.com/2.2.0/launch/previous/?limit=100'
UNION ALL
SELECT 900, 'SpaceTraffic four-week seed', 'DETERMINISTIC_SEED', 'local://space-traffic-etl/recent-four-week-seed';

INSERT INTO "dim_operator"
(
    "operator_id",
    "operator_name",
    "operator_type",
    "country"
)
SELECT
    ROW_NUMBER() OVER (ORDER BY op_src."operator_name") AS "operator_id",
    op_src."operator_name",
    op_src."operator_type",
    op_src."country"
FROM (
    SELECT
        COALESCE("operator_name", 'UNKNOWN') AS "operator_name",
        'UNKNOWN' AS "operator_type",
        MAX("country") AS "country"
    FROM (
        SELECT
            "norad_id",
            MAX("operator_name") AS "operator_name",
            MAX("country") AS "country"
        FROM "stg_ucs_satellites"
        WHERE "norad_id" IS NOT NULL
        GROUP BY "norad_id"
    ) dedup_ucs
    GROUP BY COALESCE("operator_name", 'UNKNOWN')
) op_src;

INSERT INTO "dim_orbit_band"
(
    "orbit_band_id",
    "orbit_class",
    "altitude_min_km",
    "altitude_max_km",
    "inclination_class"
)
SELECT 1, 'LEO', 160, 2000, 'LOW_INCLINATION'
UNION ALL
SELECT 2, 'MEO', 2000, 35786, 'MEDIUM_INCLINATION'
UNION ALL
SELECT 3, 'GEO', 35786, 35786, 'EQUATORIAL'
UNION ALL
SELECT 4, 'HEO', 35786, NULL, 'HIGH_INCLINATION';

INSERT INTO "dim_space_object"
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
SELECT
    c."norad_id" AS "object_id",
    c."norad_id",
    MAX(c."object_name") AS "object_name",
    CASE
        WHEN MAX(c."source_group") = 'debris' THEN 'DEBRIS'
        ELSE 'SATELLITE'
    END AS "object_type",
    MAX(u."operational_status") AS "operational_status",
    MAX(u."purpose") AS "purpose",
    MAX(u."launch_date") AS "launch_date",
    MAX(o."operator_id") AS "operator_id"
FROM (
    SELECT
        "norad_id",
        MAX("object_name") AS "object_name",
        MAX("source_group") AS "source_group"
    FROM "stg_celestrak_objects"
    WHERE "norad_id" IS NOT NULL
    GROUP BY "norad_id"
) c
LEFT JOIN "stg_ucs_satellites" u
    ON c."norad_id" = u."norad_id"
LEFT JOIN "dim_operator" o
    ON COALESCE(u."operator_name", 'UNKNOWN') = o."operator_name"
WHERE c."norad_id" IS NOT NULL
GROUP BY c."norad_id";
