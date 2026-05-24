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
    max_ids."max_observation_id" + ROW_NUMBER() OVER (ORDER BY candidates."object_id", candidates."observation_timestamp") AS "observation_id",
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
            WHEN u."orbit_type" = 'LEO' THEN 1
            WHEN u."orbit_type" = 'MEO' THEN 2
            WHEN u."orbit_type" = 'GEO' THEN 3
            WHEN u."orbit_type" = 'HEO' THEN 4
            ELSE NULL
        END AS "orbit_band_id",
        1 AS "source_id",
        c."epoch" AS "observation_timestamp",
        NULL AS "altitude_km",
        NULL AS "latitude",
        NULL AS "longitude",
        NULL AS "velocity_km_s",
        c."inclination_deg",
        1 AS "object_count",
        CASE
            WHEN c."eccentricity" >= 0.1 THEN 0.75
            WHEN c."mean_motion" >= 12 THEN 0.50
            ELSE 0.25
        END AS "debris_risk_score"
    FROM "stg_celestrak_objects" c
    JOIN "dim_space_object" d
        ON c."norad_id" = d."norad_id"
    LEFT JOIN "stg_ucs_satellites" u
        ON c."norad_id" = u."norad_id"
    WHERE c."norad_id" IS NOT NULL
      AND c."epoch" IS NOT NULL
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
