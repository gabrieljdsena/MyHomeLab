-- Shutdown and restart from the hub was removed, so the delay that only fed `shutdown /t`
-- is no longer part of any contract. Drop the column together with its check constraint.
ALTER TABLE lan_machines DROP CONSTRAINT IF EXISTS ck_lan_machines_shutdown_delay_s;
ALTER TABLE lan_machines DROP COLUMN IF EXISTS shutdown_delay_s;
