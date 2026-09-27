-- The reachability probe now records the address it resolved for each machine.
ALTER TABLE lan_machines
    ADD COLUMN IF NOT EXISTS last_ip  TEXT,
    ADD COLUMN IF NOT EXISTS last_mac TEXT;

-- Dropped first so a re-run of this script cannot trip over the constraints it already created.
ALTER TABLE lan_machines DROP CONSTRAINT IF EXISTS ck_lan_machines_last_ip;
ALTER TABLE lan_machines DROP CONSTRAINT IF EXISTS ck_lan_machines_last_mac;

ALTER TABLE lan_machines
    ADD CONSTRAINT ck_lan_machines_last_ip  CHECK (last_ip IS NULL OR last_ip ~ '^[0-9]{1,3}(\.[0-9]{1,3}){3}$'),
    ADD CONSTRAINT ck_lan_machines_last_mac CHECK (last_mac IS NULL OR last_mac ~ '^[0-9a-f]{2}(:[0-9a-f]{2}){5}$');
