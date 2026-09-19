INSERT INTO apps (name, description, url, icon, category, port, tags, health_check_enabled, health_check_interval_ms, sort_order)
VALUES
    ('Pi-hole',        'DNS + ad blocking',                 'http://192.168.15.22',   'dns',            'network',    80,   ARRAY['dns','adblock'],   TRUE, 30000, 10),
    ('Open WebUI',     'LLM chat frontend',                 'http://192.168.15.22:3000', 'forum',        'ai',         3000, ARRAY['llm','chat'],       TRUE, 30000, 20),
    ('SearXNG',        'Meta search engine',                'http://192.168.15.22:8888', 'travel_explore', 'tools',   8888, ARRAY['search'],          TRUE, 30000, 30),
    ('Jellyfin',       'Media server',                      'http://192.168.15.22:8096', 'movie',        'media',      8096, ARRAY['media','video'],    TRUE, 30000, 40),
    ('vroid server',   'Chat backend',                      'http://192.168.15.22:5000', 'chat',         'ai',         5000, ARRAY['chat','backend'],   TRUE, 30000, 50),
    ('LLM server',     'Local model inference (llama.cpp)', 'http://192.168.15.22:1234', 'memory',       'ai',         1234, ARRAY['llm','inference'],  TRUE, 30000, 60)
ON CONFLICT (name) DO NOTHING;