OPEN SCHEMA ${SCHEMA};

DELETE FROM "fact_object_position_observation"
WHERE "source_id" = 900
  AND CAST("observation_timestamp" AS DATE) < ADD_DAYS(CURRENT_DATE, -27);

DELETE FROM "fact_launch_event"
WHERE "source_id" = 900;

DELETE FROM "fact_object_position_observation"
WHERE "observation_id" IN (
    SELECT seed_observation."observation_id"
    FROM "fact_object_position_observation" seed_observation
    JOIN "fact_object_position_observation" real_observation
        ON real_observation."object_id" = seed_observation."object_id"
       AND real_observation."source_id" <> 900
       AND CAST(real_observation."observation_timestamp" AS DATE) = CAST(seed_observation."observation_timestamp" AS DATE)
    WHERE seed_observation."source_id" = 900
);

DELETE FROM "fact_launch_event"
WHERE "launch_id" IN (
    SELECT seed_launch."launch_id"
    FROM "fact_launch_event" seed_launch
    JOIN "fact_launch_event" real_launch
        ON real_launch."source_id" <> 900
       AND CAST(real_launch."launch_timestamp" AS DATE) = CAST(seed_launch."launch_timestamp" AS DATE)
    WHERE seed_launch."source_id" = 900
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
    max_ids."max_launch_event_id" + ROW_NUMBER() OVER (ORDER BY seed_launches."launch_timestamp") AS "launch_event_id",
    seed_launches."launch_id",
    900 AS "source_id",
    seed_launches."launch_timestamp",
    seed_launches."launch_name",
    'Abgeleitete Daten' AS "provider_name",
    seed_launches."rocket_name",
    seed_launches."launch_country",
    'Abgeleitete Daten' AS "launch_status",
    seed_launches."launch_site",
    seed_launches."payload_count",
    seed_launches."success_count"
FROM (
    SELECT
        'abgeleitete-daten-launch-' || TO_CHAR(CAST(ADD_DAYS(CURRENT_DATE, -24) AS DATE), 'YYYYMMDD') AS "launch_id",
        CAST(ADD_DAYS(CURRENT_DATE, -24) AS TIMESTAMP) AS "launch_timestamp",
        'Abgeleitete Launch-Aktivitaet Woche 1' AS "launch_name",
        'Representative orbital mission' AS "rocket_name",
        'N/A' AS "launch_country",
        'Abgeleiteter Startplatz' AS "launch_site",
        1 AS "payload_count",
        1 AS "success_count"
    UNION ALL
    SELECT
        'abgeleitete-daten-launch-' || TO_CHAR(CAST(ADD_DAYS(CURRENT_DATE, -17) AS DATE), 'YYYYMMDD'),
        CAST(ADD_DAYS(CURRENT_DATE, -17) AS TIMESTAMP),
        'Abgeleitete Launch-Aktivitaet Woche 2',
        'Representative orbital mission',
        'N/A',
        'Abgeleiteter Startplatz',
        1,
        1
    UNION ALL
    SELECT
        'abgeleitete-daten-launch-' || TO_CHAR(CAST(ADD_DAYS(CURRENT_DATE, -10) AS DATE), 'YYYYMMDD'),
        CAST(ADD_DAYS(CURRENT_DATE, -10) AS TIMESTAMP),
        'Abgeleitete Launch-Aktivitaet Woche 3',
        'Representative orbital mission',
        'N/A',
        'Abgeleiteter Startplatz',
        1,
        1
    UNION ALL
    SELECT
        'abgeleitete-daten-launch-' || TO_CHAR(CAST(ADD_DAYS(CURRENT_DATE, -3) AS DATE), 'YYYYMMDD'),
        CAST(ADD_DAYS(CURRENT_DATE, -3) AS TIMESTAMP),
        'Abgeleitete Launch-Aktivitaet Woche 4',
        'Representative orbital mission',
        'N/A',
        'Abgeleiteter Startplatz',
        1,
        1
) seed_launches
CROSS JOIN (
    SELECT COALESCE(MAX("launch_event_id"), 0) AS "max_launch_event_id"
    FROM "fact_launch_event"
) max_ids
WHERE NOT EXISTS (
    SELECT 1
    FROM "fact_launch_event" existing
    WHERE existing."launch_id" = seed_launches."launch_id"
)
AND NOT EXISTS (
    SELECT 1
    FROM "fact_launch_event" real_launch
    WHERE real_launch."source_id" <> 900
      AND CAST(real_launch."launch_timestamp" AS DATE) = CAST(seed_launches."launch_timestamp" AS DATE)
);

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
    900 AS "source_id",
    candidates."observation_timestamp",
    candidates."altitude_km",
    NULL AS "latitude",
    NULL AS "longitude",
    candidates."velocity_km_s",
    candidates."inclination_deg",
    1 AS "object_count",
    candidates."debris_risk_score"
FROM (
    SELECT
        objects."object_id",
        objects."orbit_band_id",
        seed_days."observation_timestamp",
        CASE
            WHEN objects."orbit_band_id" = 1 THEN 550
            WHEN objects."orbit_band_id" = 2 THEN 20200
            WHEN objects."orbit_band_id" = 3 THEN 35786
            WHEN objects."orbit_band_id" = 4 THEN 42000
            ELSE NULL
        END AS "altitude_km",
        CASE
            WHEN objects."orbit_band_id" = 1 THEN 7.8
            WHEN objects."orbit_band_id" = 2 THEN 3.9
            WHEN objects."orbit_band_id" = 3 THEN 3.1
            WHEN objects."orbit_band_id" = 4 THEN 2.5
            ELSE NULL
        END AS "velocity_km_s",
        objects."inclination_deg",
        objects."debris_risk_score"
    FROM (
        SELECT
            d."object_id",
            COALESCE(
                MAX(CASE
                    WHEN u."orbit_type" = 'LEO' THEN 1
                    WHEN u."orbit_type" = 'MEO' THEN 2
                    WHEN u."orbit_type" = 'GEO' THEN 3
                    WHEN u."orbit_type" = 'HEO' THEN 4
                    ELSE NULL
                END),
                MAX(CASE
                    WHEN c."source_group" = 'geo' THEN 3
                    ELSE NULL
                END)
            ) AS "orbit_band_id",
            MAX(c."inclination_deg") AS "inclination_deg",
            CASE
                WHEN MAX(d."object_type") = 'DEBRIS' THEN 0.90
                WHEN MAX(c."eccentricity") >= 0.1 THEN 0.75
                WHEN MAX(c."mean_motion") >= 12 THEN 0.50
                ELSE 0.25
            END AS "debris_risk_score"
        FROM "dim_space_object" d
        LEFT JOIN "stg_ucs_satellites" u
            ON d."norad_id" = u."norad_id"
        LEFT JOIN "stg_celestrak_objects" c
            ON d."norad_id" = c."norad_id"
        GROUP BY d."object_id"
    ) objects
    CROSS JOIN (
        SELECT CAST(ADD_DAYS(CURRENT_DATE, -27) AS TIMESTAMP) AS "observation_timestamp"
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -26) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -25) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -24) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -23) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -22) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -21) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -20) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -19) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -18) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -17) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -16) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -15) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -14) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -13) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -12) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -11) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -10) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -9) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -8) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -7) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -6) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -5) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -4) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -3) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -2) AS TIMESTAMP)
        UNION ALL SELECT CAST(ADD_DAYS(CURRENT_DATE, -1) AS TIMESTAMP)
        UNION ALL SELECT CAST(CURRENT_DATE AS TIMESTAMP)
    ) seed_days
) candidates
CROSS JOIN (
    SELECT COALESCE(MAX("observation_id"), 0) AS "max_observation_id"
    FROM "fact_object_position_observation"
) max_ids
WHERE NOT EXISTS (
    SELECT 1
    FROM "fact_object_position_observation" existing
    WHERE existing."object_id" = candidates."object_id"
      AND existing."source_id" = 900
      AND existing."observation_timestamp" = candidates."observation_timestamp"
)
AND NOT EXISTS (
    SELECT 1
    FROM "fact_object_position_observation" real_observation
    WHERE real_observation."object_id" = candidates."object_id"
      AND real_observation."source_id" <> 900
      AND CAST(real_observation."observation_timestamp" AS DATE) = CAST(candidates."observation_timestamp" AS DATE)
);
