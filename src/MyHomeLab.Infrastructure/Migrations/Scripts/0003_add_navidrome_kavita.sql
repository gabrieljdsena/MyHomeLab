INSERT INTO apps (name, description, url, icon, category, port, tags, health_check_enabled, health_check_interval_ms, sort_order)
VALUES
    ('Navidrome', 'Music server',                          'http://192.168.15.22:4533', 'music_note', 'media',    4533, ARRAY['music'],          TRUE, 30000, 45),
    ('Kavita',    'Ebook, comic & manga reader',           'http://192.168.15.22:5001', 'book',       'media',    5001, ARRAY['books','reader'],  TRUE, 30000, 46)
ON CONFLICT (name) DO NOTHING;