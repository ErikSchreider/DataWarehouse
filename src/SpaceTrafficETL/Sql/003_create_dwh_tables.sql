OPEN SCHEMA ${SCHEMA};

CREATE TABLE "dim_operator"
(
    "operator_id" DECIMAL(18,0),
    "operator_name" VARCHAR(512),
    "operator_type" VARCHAR(128),
    "country" VARCHAR(256)
);

CREATE TABLE "dim_space_object"
(
    "object_id" DECIMAL(18,0),
    "norad_id" DECIMAL(18,0),
    "object_name" VARCHAR(512),
    "object_type" VARCHAR(128),
    "operational_status" VARCHAR(128),
    "purpose" VARCHAR(512),
    "launch_date" DATE,
    "operator_id" DECIMAL(18,0)
);

CREATE TABLE "dim_orbit_band"
(
    "orbit_band_id" DECIMAL(18,0),
    "orbit_class" VARCHAR(128),
    "altitude_min_km" DOUBLE,
    "altitude_max_km" DOUBLE,
    "inclination_class" VARCHAR(128)
);

CREATE TABLE "dim_source"
(
    "source_id" DECIMAL(18,0),
    "source_name" VARCHAR(256),
    "source_type" VARCHAR(128),
    "source_url" VARCHAR(2048)
);

CREATE TABLE "fact_object_position_observation"
(
    "observation_id" DECIMAL(18,0),
    "object_id" DECIMAL(18,0),
    "orbit_band_id" DECIMAL(18,0),
    "source_id" DECIMAL(18,0),
    "observation_timestamp" TIMESTAMP,
    "altitude_km" DOUBLE,
    "latitude" DOUBLE,
    "longitude" DOUBLE,
    "velocity_km_s" DOUBLE,
    "inclination_deg" DOUBLE,
    "object_count" DECIMAL(18,0),
    "debris_risk_score" DOUBLE
);

CREATE TABLE "fact_launch_event"
(
    "launch_event_id" DECIMAL(18,0),
    "source_id" DECIMAL(18,0),
    "launch_timestamp" TIMESTAMP,
    "launch_name" VARCHAR(512),
    "provider_name" VARCHAR(512),
    "launch_site" VARCHAR(512),
    "payload_count" DECIMAL(18,0)
);
