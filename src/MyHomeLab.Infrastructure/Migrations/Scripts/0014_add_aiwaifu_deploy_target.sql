-- Point the AI Waifu Assistant tile at its Windows service + deploy script.
-- The app lives in C:\Users\gabriel\ai-waifu-assistant and is registered as the
-- service 'AIWaifuAssistant' via its scripts\install-service.ps1 (NSSM); deploys
-- run scripts\build.ps1 (pip install, then service restart).
UPDATE apps
SET service_name = 'AIWaifuAssistant',
    build_script_path = 'C:\Users\gabriel\ai-waifu-assistant\scripts\build.ps1'
WHERE lower(name) LIKE 'ai waifu%';
