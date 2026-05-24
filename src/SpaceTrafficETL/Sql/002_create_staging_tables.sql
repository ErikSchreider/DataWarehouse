OPEN SCHEMA ${SCHEMA};

CREATE TABLE "stg_celestrak_objects"
(
    "object_name" VARCHAR(512),
    "norad_id" DECIMAL(18,0),
    "tle_line1" VARCHAR(256),
    "tle_line2" VARCHAR(256),
    "source_group" VARCHAR(128),
    "epoch" TIMESTAMP,
    "inclination_deg" DOUBLE,
    "eccentricity" DOUBLE,
    "mean_motion" DOUBLE,
    "imported_at" TIMESTAMP
);

CREATE TABLE "stg_ucs_satellites"
(
    "norad_id" DECIMAL(18,0),
    "satellite_name" VARCHAR(512),
    "country" VARCHAR(256),
    "operator_name" VARCHAR(512),
    "purpose" VARCHAR(512),
    "orbit_type" VARCHAR(128),
    "operational_status" VARCHAR(128),
    "launch_date" DATE,
    "imported_at" TIMESTAMP
);
