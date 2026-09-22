CREATE TABLE IF NOT EXISTS app_health_checks (
    id         BIGSERIAL PRIMARY KEY,
    app_id     UUID NOT NULL REFERENCES apps(id) ON DELETE CASCADE,
    status     TEXT NOT NULL CHECK (status IN ('unknown','up','down')),
    latency_ms INTEGER CHECK (latency_ms IS NULL OR latency_ms >= 0),
    checked_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_app_health_checks_app_time ON app_health_checks (app_id, checked_at DESC);
CREATE INDEX IF NOT EXISTS ix_app_health_checks_checked_at ON app_health_checks (checked_at DESC);
