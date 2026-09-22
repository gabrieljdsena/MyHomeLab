ALTER TABLE apps ADD COLUMN IF NOT EXISTS docker_container TEXT;
CREATE INDEX IF NOT EXISTS ix_apps_docker_container ON apps (docker_container) WHERE docker_container IS NOT NULL;
