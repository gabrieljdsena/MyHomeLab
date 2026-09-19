INSERT INTO apps (name, description, url, icon, category, port, tags, health_check_enabled, health_check_interval_ms, sort_order)
VALUES (
    'RomM',
    'ROM manager & player — zip library, metadata, browser EmulatorJS',
    'http://192.168.15.22:8082',
    'sports_esports',
    'media',
    8082,
    ARRAY['games','roms','emulator','retro'],
    TRUE, 30000, 47
)
ON CONFLICT (name) DO UPDATE SET
    description = EXCLUDED.description,
    url = EXCLUDED.url,
    icon = EXCLUDED.icon,
    category = EXCLUDED.category,
    port = EXCLUDED.port,
    tags = EXCLUDED.tags;
