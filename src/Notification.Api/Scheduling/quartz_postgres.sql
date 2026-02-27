-- Quartz.NET PostgreSQL schema
-- Adapted from https://github.com/quartznet/quartznet/blob/main/database/tables/tables_postgres.sql
-- Using CREATE TABLE IF NOT EXISTS for idempotent startup execution

CREATE TABLE IF NOT EXISTS qrtz_job_details (
    sched_name        TEXT NOT NULL,
    job_name          TEXT NOT NULL,
    job_group         TEXT NOT NULL,
    description       TEXT NULL,
    job_class_name    TEXT NOT NULL,
    is_durable        BOOL NOT NULL,
    is_nonconcurrent  BOOL NOT NULL,
    is_update_data    BOOL NOT NULL,
    requests_recovery BOOL NOT NULL,
    job_data          BYTEA NULL,
    CONSTRAINT pk_qrtz_job_details PRIMARY KEY (sched_name, job_name, job_group)
);

CREATE TABLE IF NOT EXISTS qrtz_triggers (
    sched_name     TEXT NOT NULL,
    trigger_name   TEXT NOT NULL,
    trigger_group  TEXT NOT NULL,
    job_name       TEXT NOT NULL,
    job_group      TEXT NOT NULL,
    description    TEXT NULL,
    next_fire_time BIGINT NULL,
    prev_fire_time BIGINT NULL,
    priority       INTEGER NULL,
    trigger_state  TEXT NOT NULL,
    trigger_type   TEXT NOT NULL,
    start_time     BIGINT NOT NULL,
    end_time       BIGINT NULL,
    calendar_name  TEXT NULL,
    misfire_instr  SMALLINT NULL,
    job_data       BYTEA NULL,
    CONSTRAINT pk_qrtz_triggers PRIMARY KEY (sched_name, trigger_name, trigger_group),
    CONSTRAINT fk_qrtz_triggers_job_details FOREIGN KEY (sched_name, job_name, job_group)
        REFERENCES qrtz_job_details (sched_name, job_name, job_group)
);

CREATE TABLE IF NOT EXISTS qrtz_simple_triggers (
    sched_name         TEXT NOT NULL,
    trigger_name       TEXT NOT NULL,
    trigger_group      TEXT NOT NULL,
    repeat_count       BIGINT NOT NULL,
    repeat_interval    BIGINT NOT NULL,
    times_triggered    BIGINT NOT NULL,
    CONSTRAINT pk_qrtz_simple_triggers PRIMARY KEY (sched_name, trigger_name, trigger_group),
    CONSTRAINT fk_qrtz_simple_triggers FOREIGN KEY (sched_name, trigger_name, trigger_group)
        REFERENCES qrtz_triggers (sched_name, trigger_name, trigger_group)
);

CREATE TABLE IF NOT EXISTS qrtz_simprop_triggers (
    sched_name        TEXT NOT NULL,
    trigger_name      TEXT NOT NULL,
    trigger_group     TEXT NOT NULL,
    str_prop_1        TEXT NULL,
    str_prop_2        TEXT NULL,
    str_prop_3        TEXT NULL,
    int_prop_1        INT NULL,
    int_prop_2        INT NULL,
    long_prop_1       BIGINT NULL,
    long_prop_2       BIGINT NULL,
    dec_prop_1        NUMERIC NULL,
    dec_prop_2        NUMERIC NULL,
    bool_prop_1       BOOL NULL,
    bool_prop_2       BOOL NULL,
    time_zone_id      TEXT NULL,
    CONSTRAINT pk_qrtz_simprop_triggers PRIMARY KEY (sched_name, trigger_name, trigger_group),
    CONSTRAINT fk_qrtz_simprop_triggers FOREIGN KEY (sched_name, trigger_name, trigger_group)
        REFERENCES qrtz_triggers (sched_name, trigger_name, trigger_group)
);

CREATE TABLE IF NOT EXISTS qrtz_cron_triggers (
    sched_name      TEXT NOT NULL,
    trigger_name    TEXT NOT NULL,
    trigger_group   TEXT NOT NULL,
    cron_expression TEXT NOT NULL,
    time_zone_id    TEXT NULL,
    CONSTRAINT pk_qrtz_cron_triggers PRIMARY KEY (sched_name, trigger_name, trigger_group),
    CONSTRAINT fk_qrtz_cron_triggers FOREIGN KEY (sched_name, trigger_name, trigger_group)
        REFERENCES qrtz_triggers (sched_name, trigger_name, trigger_group)
);

CREATE TABLE IF NOT EXISTS qrtz_blob_triggers (
    sched_name    TEXT NOT NULL,
    trigger_name  TEXT NOT NULL,
    trigger_group TEXT NOT NULL,
    blob_data     BYTEA NULL,
    CONSTRAINT pk_qrtz_blob_triggers PRIMARY KEY (sched_name, trigger_name, trigger_group),
    CONSTRAINT fk_qrtz_blob_triggers FOREIGN KEY (sched_name, trigger_name, trigger_group)
        REFERENCES qrtz_triggers (sched_name, trigger_name, trigger_group)
);

CREATE TABLE IF NOT EXISTS qrtz_calendars (
    sched_name    TEXT NOT NULL,
    calendar_name TEXT NOT NULL,
    calendar      BYTEA NOT NULL,
    CONSTRAINT pk_qrtz_calendars PRIMARY KEY (sched_name, calendar_name)
);

CREATE TABLE IF NOT EXISTS qrtz_paused_trigger_grps (
    sched_name    TEXT NOT NULL,
    trigger_group TEXT NOT NULL,
    CONSTRAINT pk_qrtz_paused_trigger_grps PRIMARY KEY (sched_name, trigger_group)
);

CREATE TABLE IF NOT EXISTS qrtz_fired_triggers (
    sched_name        TEXT NOT NULL,
    entry_id          TEXT NOT NULL,
    trigger_name      TEXT NOT NULL,
    trigger_group     TEXT NOT NULL,
    instance_name     TEXT NOT NULL,
    fired_time        BIGINT NOT NULL,
    sched_time        BIGINT NOT NULL,
    priority          INTEGER NOT NULL,
    state             TEXT NOT NULL,
    job_name          TEXT NULL,
    job_group         TEXT NULL,
    is_nonconcurrent  BOOL NULL,
    requests_recovery BOOL NULL,
    CONSTRAINT pk_qrtz_fired_triggers PRIMARY KEY (sched_name, entry_id)
);

CREATE TABLE IF NOT EXISTS qrtz_scheduler_state (
    sched_name        TEXT NOT NULL,
    instance_name     TEXT NOT NULL,
    last_checkin_time BIGINT NOT NULL,
    checkin_interval  BIGINT NOT NULL,
    CONSTRAINT pk_qrtz_scheduler_state PRIMARY KEY (sched_name, instance_name)
);

CREATE TABLE IF NOT EXISTS qrtz_locks (
    sched_name TEXT NOT NULL,
    lock_name  TEXT NOT NULL,
    CONSTRAINT pk_qrtz_locks PRIMARY KEY (sched_name, lock_name)
);

CREATE INDEX IF NOT EXISTS idx_qrtz_j_req_recovery ON qrtz_job_details (sched_name, requests_recovery);
CREATE INDEX IF NOT EXISTS idx_qrtz_t_next_fire_time ON qrtz_triggers (sched_name, next_fire_time);
CREATE INDEX IF NOT EXISTS idx_qrtz_t_state ON qrtz_triggers (sched_name, trigger_state);
CREATE INDEX IF NOT EXISTS idx_qrtz_t_nft_st ON qrtz_triggers (sched_name, trigger_state, next_fire_time);
CREATE INDEX IF NOT EXISTS idx_qrtz_ft_trig_inst_name ON qrtz_fired_triggers (sched_name, instance_name);
CREATE INDEX IF NOT EXISTS idx_qrtz_ft_inst_job_req_rcvry ON qrtz_fired_triggers (sched_name, requests_recovery);
CREATE INDEX IF NOT EXISTS idx_qrtz_ft_j_g ON qrtz_fired_triggers (sched_name, job_name, job_group);
CREATE INDEX IF NOT EXISTS idx_qrtz_ft_tg ON qrtz_fired_triggers (sched_name, trigger_group);
