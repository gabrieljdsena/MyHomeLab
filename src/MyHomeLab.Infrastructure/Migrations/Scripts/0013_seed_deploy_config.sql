-- Deploy targets for the two self-hosted apps (0013_seed_deploy_config.sql)
UPDATE apps
SET service_name      = 'Hathor',
    build_script_path = 'C:\Users\gabriel\code\Hathor\apps\web\scripts\build.ps1'
WHERE lower(name) = 'hathor';

INSERT INTO apps (name, description, url, icon, category, port, tags, docker_container, service_name, build_script_path, health_check_enabled, health_check_interval_ms, sort_order)
VALUES ('MyHomeLab', 'Self-hosted hub (this dashboard)', 'https://192.168.15.22', 'hub', 'tools', 443, ARRAY['hub','dashboard'], NULL, 'MyHomeLab', 'C:\Users\gabriel\myhomelab\scripts\build.ps1', TRUE, 30000, 5)
ON CONFLICT (name) DO UPDATE SET
    service_name      = EXCLUDED.service_name,
    build_script_path = EXCLUDED.build_script_path;
