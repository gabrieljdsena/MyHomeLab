CREATE TABLE IF NOT EXISTS apps (
    id                        UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name                      TEXT NOT NULL UNIQUE,
    description               TEXT NOT NULL DEFAULT '',
    url                       TEXT NOT NULL,
    icon                      TEXT NOT NULL DEFAULT 'web',
    category                  TEXT NOT NULL DEFAULT 'other',
    port                      INTEGER,
    tags                      TEXT[] NOT NULL DEFAULT '{}',
    health_check_enabled      BOOLEAN NOT NULL DEFAULT TRUE,
    health_check_interval_ms  INTEGER NOT NULL DEFAULT 30000,
    health_status             TEXT NOT NULL DEFAULT 'unknown',
    last_health_check         TIMESTAMPTZ,
    last_latency_ms           INTEGER,
    is_enabled                BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order                INTEGER NOT NULL DEFAULT 0,
    created_at                TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at                TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_apps_category ON apps (category);
CREATE INDEX IF NOT EXISTS ix_apps_is_enabled ON apps (is_enabled);