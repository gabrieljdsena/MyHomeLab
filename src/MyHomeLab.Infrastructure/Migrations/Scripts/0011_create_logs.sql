CREATE TABLE IF NOT EXISTS logs (
    id          INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    application VARCHAR(255) NOT NULL,
    log         TEXT NOT NULL DEFAULT ''
);

CREATE INDEX IF NOT EXISTS ix_logs_application ON logs (application);
CREATE INDEX IF NOT EXISTS ix_logs_id_desc ON logs (id DESC);
