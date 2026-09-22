INSERT INTO apps (name, description, url, icon, category, port, tags, docker_container, health_check_enabled, health_check_interval_ms, sort_order)
VALUES
    ('Vaultwarden', 'Password manager (Bitwarden)', 'https://192.168.15.22:3012', 'vpn_key', 'tools',   3012, ARRAY['password','bitwarden'], 'vaultwarden', TRUE, 30000, 70),
    ('Syncthing',   'File sync & backup',           'http://192.168.15.22:8384', 'sync',    'storage', 8384, ARRAY['sync','backup'],        'syncthing',   TRUE, 30000, 71)
ON CONFLICT (name) DO UPDATE SET
    description = EXCLUDED.description,
    url = EXCLUDED.url,
    icon = EXCLUDED.icon,
    category = EXCLUDED.category,
    port = EXCLUDED.port,
    tags = EXCLUDED.tags,
    docker_container = EXCLUDED.docker_container;
