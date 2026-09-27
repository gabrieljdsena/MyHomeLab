CREATE TABLE IF NOT EXISTS lan_machines (
    id                 UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name               TEXT NOT NULL UNIQUE,
    description        TEXT NOT NULL DEFAULT '',
    hostname           TEXT NOT NULL,
    icon               TEXT NOT NULL DEFAULT 'computer',
    shutdown_delay_s   INTEGER NOT NULL DEFAULT 5,
    is_enabled         BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order         INTEGER NOT NULL DEFAULT 0,
    reachability       TEXT NOT NULL DEFAULT 'unknown',
    last_seen          TIMESTAMPTZ,
    last_latency_ms    INTEGER,
    created_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_lan_machines_shutdown_delay_s CHECK (shutdown_delay_s BETWEEN 0 AND 300),
    CONSTRAINT ck_lan_machines_hostname CHECK (hostname ~* '^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)*$'),
    CONSTRAINT ck_lan_machines_reachability CHECK (reachability IN ('unknown', 'online', 'offline'))
);

CREATE INDEX IF NOT EXISTS ix_lan_machines_is_enabled ON lan_machines (is_enabled);
CREATE INDEX IF NOT EXISTS ix_lan_machines_sort_order ON lan_machines (sort_order, name);
