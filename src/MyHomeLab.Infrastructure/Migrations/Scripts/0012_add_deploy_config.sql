-- Deploy targets: per-app build script + Windows service (0012_add_deploy_config.sql)
ALTER TABLE apps
    ADD COLUMN IF NOT EXISTS service_name      TEXT,
    ADD COLUMN IF NOT EXISTS build_script_path TEXT;
