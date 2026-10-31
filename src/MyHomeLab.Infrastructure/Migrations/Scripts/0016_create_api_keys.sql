CREATE TABLE IF NOT EXISTS api_keys (
    id          UUID PRIMARY KEY,
    name        TEXT NOT NULL,
    prefix      TEXT NOT NULL,
    token_hash  TEXT NOT NULL UNIQUE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_used_at TIMESTAMPTZ,
    revoked_at  TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS ix_api_keys_token_hash ON api_keys (token_hash);
CREATE INDEX IF NOT EXISTS ix_api_keys_revoked_at ON api_keys (revoked_at);
