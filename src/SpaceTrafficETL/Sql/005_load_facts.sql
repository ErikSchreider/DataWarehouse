OPEN SCHEMA ${SCHEMA};

INSERT INTO "fact_object_position_observation"
(
    "observation_id",
    "object_id",
    "orbit_band_id",
    "source_id",
    "observation_timestamp",
    "altitude_km",
    "latitude",
    "longitude",
    "velocity_km_s",
    "inclination_deg",
    "object_count",
    "debris_risk_score"
)
SELECT
    max_ids."max_observation_id" + ROW_NUMBER() OVER (ORDER BY candidates."object_id", candidates."observation_timestamp", candidates."source_id") AS "observation_id",
    candidates."object_id",
    candidates."orbit_band_id",
    candidates."source_id",
    candidates."observation_timestamp",
    candidates."altitude_km",
    candidates."latitude",
    candidates."longitude",
    candidates."velocity_km_s",
    candidates."inclination_deg",
    candidates."object_count",
    candidates."debris_risk_score"
FROM (
    SELECT
        d."object_id",
        CASE
            WHEN MAX(u."orbit_type") = 'LEO' THEN 1
            WHEN MAX(u."orbit_type") = 'MEO' THEN 2
            WHEN MAX(u."orbit_type") = 'GEO' THEN 3
            WHEN MAX(u."orbit_type") = 'HEO' THEN 4
            WHEN c."source_group" = 'geo' THEN 3
            ELSE NULL
        END AS "orbit_band_id",
        s."source_id",
        c."epoch" AS "observation_timestamp",
        NULL AS "altitude_km",
        NULL AS "latitude",
        NULL AS "longitude",
        NULL AS "velocity_km_s",
        MAX(c."inclination_deg") AS "inclination_deg",
        1 AS "object_count",
        CASE
            WHEN c."source_group" = 'debris' THEN 0.90
            WHEN MAX(c."eccentricity") >= 0.1 THEN 0.75
            WHEN MAX(c."mean_motion") >= 12 THEN 0.50
            ELSE 0.25
        END AS "debris_risk_score"
    FROM "stg_celestrak_objects" c
    JOIN "dim_space_object" d
        ON c."norad_id" = d."norad_id"
    JOIN "dim_source" s
        ON s."source_name" = 'CelesTrak ' || c."source_group"
    LEFT JOIN "stg_ucs_satellites" u
        ON c."norad_id" = u."norad_id"
    WHERE c."norad_id" IS NOT NULL
      AND c."epoch" IS NOT NULL
      AND c."source_group" IS NOT NULL
    GROUP BY
        d."object_id",
        s."source_id",
        c."epoch",
        c."source_group"
) candidates
CROSS JOIN (
    SELECT COALESCE(MAX("observation_id"), 0) AS "max_observation_id"
    FROM "fact_object_position_observation"
) max_ids
WHERE NOT EXISTS (
    SELECT 1
    FROM "fact_object_position_observation" existing
    WHERE existing."object_id" = candidates."object_id"
      AND existing."source_id" = candidates."source_id"
      AND existing."observation_timestamp" = candidates."observation_timestamp"
);

INSERT INTO "fact_launch_event"
(
    "launch_event_id",
    "launch_id",
    "source_id",
    "launch_timestamp",
    "launch_name",
    "provider_name",
    "rocket_name",
    "launch_country",
    "launch_status",
    "launch_site",
    "payload_count",
    "success_count"
)
SELECT
    max_ids."max_launch_event_id" + ROW_NUMBER() OVER (ORDER BY candidates."launch_id") AS "launch_event_id",
    candidates."launch_id",
    candidates."source_id",
    candidates."launch_timestamp",
    candidates."launch_name",
    candidates."provider_name",
    candidates."rocket_name",
    candidates."launch_country",
    candidates."launch_status",
    NULL AS "launch_site",
    candidates."payload_count",
    candidates."success_count"
FROM (
    SELECT
        l."launch_id",
        200 AS "source_id",
        MAX(l."launch_date") AS "launch_timestamp",
        MAX(l."launch_name") AS "launch_name",
        MAX(l."launch_provider") AS "provider_name",
        MAX(l."rocket_name") AS "rocket_name",
        MAX(l."launch_country") AS "launch_country",
        MAX(l."launch_status") AS "launch_status",
        MAX(l."payload_count") AS "payload_count",
        CASE
            WHEN UPPER(MAX(COALESCE(l."launch_status", ''))) LIKE '%SUCCESS%' THEN 1
            ELSE 0
        END AS "success_count"
    FROM "stg_launch_events" l
    WHERE l."launch_id" IS NOT NULL
    GROUP BY l."launch_id"
) candidates
CROSS JOIN (
    SELECT COALESCE(MAX("launch_event_id"), 0) AS "max_launch_event_id"
    FROM "fact_launch_event"
) max_ids
WHERE NOT EXISTS (
    SELECT 1
    FROM "fact_launch_event" existing
    WHERE existing."launch_id" = candidates."launch_id"
);
