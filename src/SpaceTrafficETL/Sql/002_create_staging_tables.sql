OPEN SCHEMA ${SCHEMA};

CREATE TABLE "stg_celestrak_objects"
(
    "object_name" VARCHAR(512),
    "norad_id" DECIMAL(18,0),
    "epoch" TIMESTAMP,
    "inclination_deg" DOUBLE,
    "eccentricity" DOUBLE,
    "mean_motion" DOUBLE,
    "source_group" VARCHAR(128),
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

CREATE TABLE "stg_launch_events"
(
    "launch_id" VARCHAR(128),
    "launch_name" VARCHAR(512),
    "launch_date" TIMESTAMP,
    "launch_provider" VARCHAR(512),
    "rocket_name" VARCHAR(512),
    "launch_country" VARCHAR(128),
    "launch_status" VARCHAR(128),
    "payload_count" DECIMAL(18,0),
    "imported_at" TIMESTAMP
);
