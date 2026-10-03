# MyHomeLab — Implementation Document

A self-hosted hub that gives one clean dashboard to every application running on
this server. Apps are managed through a CRUD API + UI (add, edit, remove, health-check),
so the hub stays in sync with reality.

Current server inventory (seed data):

| App        | Port | Type            | Notes                          |
|------------|------|-----------------|--------------------------------|
| Pi-hole    | 80   | Docker          | DNS + ad blocking              |
| Open WebUI | 3000 | Docker          | LLM chat                       |
| SearXNG    | 8888 | Docker          | Meta search engine             |
| Jellyfin   | 8096 | Native (Win)    | Media server                   |
| vroid server | 5000 | Native (Python) | Chat backend                   |
| LLM server | 1234 | Native          | llama.cpp / OpenAI-compatible  |

---

## 1. Goals

- **One origin page** for every lab service, with live up/down + latency status.
- **Full CRUD** over the app registry persisted to Postgres — add/remove/edit apps
  without touching code.
- **Single deployable**: one .NET process serves the React build **and** the REST API.
- **DDD + Dapper**: clean layered architecture, raw SQL, no ORM, no magic.

## 2. Ports & TLS

| Port | Protocol | Serves                                   |
|------|----------|------------------------------------------|
| 443  | HTTPS    | React SPA (static build) **and** API (`/api/*`) on same origin |
| 8080 | HTTP     | API only (for scripts / curl / apps) — confirmed unused         |

- Port 80 is owned by Pi-hole (Docker) — never bind to it.
- Kestrel binds **both** endpoints from one ASP.NET Core app (see `appsettings.json`).
- **TLS**: self-signed cert. Recommended path: `mkcert` with a CA you trust on
  your LAN machines, so `https://myhomelab` shows no warnings.
  Fallback: `dotnet dev-certs https --trust` (dev only).
  Cert files are configured via env vars (`ASPNETCORE_Kestrel__Certificates__Default__Path/Password`).

## 3. Tech Stack

**Backend**
- .NET 10 (SDK 10.0.401 installed), ASP.NET Core Web API (minimal hosting model)
- Dapper 2.x + Npgsql — everything DB is raw SQL
- Serilog (console + file, plus Postgres `logs` table for `Error`/`Fatal` via `PostgresLogSink`) — structured logging
- Swagger UI (mapped to `/swagger`, enabled always on a home lab)
- No EF Core, no MediatR, no automapper — keep the dependency surface tiny

**Frontend**
- Vite + React 18 + TypeScript
- Tailwind CSS v4 (utility-first, dark-capable)
- React Router v7, TanStack Query v5 (server state + caching)
- `fetch`-based thin API client, no axios needed

## 4. Repository Layout

```
myhomelab/
├── AGENTS.md                 # codebase rules (read by opencode)
├── README.md
├── docs/implementation.md    # this file
├── scripts/
│   ├── dev.ps1               # run API + build/watch frontend
│   ├── build.ps1             # publish API including frontend build
│   ├── install-service.ps1   # register API as a Windows service
│   └── create-db.ps1         # create Postgres database + user
├── src/
│   ├── MyHomeLab.Domain/          # entities, value objects — no deps
│   ├── MyHomeLab.Application/     # DTOs, services, repository interface
│   ├── MyHomeLab.Infrastructure/  # Dapper repo, migrations, health checker
│   └── MyHomeLab.Api/             # controllers, Kestrel config, static files
└── frontend/                       # Vite React app
    ├── src/
    │   ├── api/               # typed API client
    │   ├── components/        # UI components
    │   ├── features/apps/     # app card grid + CRUD forms
    │   ├── features/machines/ # LAN machine reachability registry
    │   ├── pages/             # Dashboard, Manage, Machines, Logs, Postgres, Terminal
    │   └── lib/               # query keys, utils, types
    └── dist/                  # Vite build output (copied to API wwwroot)
```

## 5. Layering (DDD)

Dependencies flow one way: **Api → Application → Domain**, with Infrastructure
plugs in behind interfaces.

- **Domain** (`MyHomeLab.Domain`) — pure C#. `App` aggregate:
  - identity `AppId` (UUID) as a Value Object
  - properties: `Name`, `Description`, `Url`, `Icon`, `Category`, `Port`, `Tags`, `DockerContainer`, `HealthCheckEnabled`, `HealthCheckIntervalMs`, `IsEnabled`, `SortOrder`
  - behavior (not anemic): `Enable()`, `Disable()`, `SetHealth(status, latencyMs)`,
    `UpdateDetails(...)` guard invariants (non-empty name, valid absolute URL, docker name `^[a-zA-Z0-9][a-zA-Z0-9_.\-]*$`)
  - value objects: `AppId`, `AppHealthStatus` (`Unknown | Up | Down`), `Category`
  - history entity: `AppHealthSample` (`AppId`, `Status`, `LatencyMs`, `CheckedAtUtc`) — one row per probe
  - second aggregate: `Machine` (LAN Windows PC) — identity `MachineId`, properties `Name`, `Description`,
    `Hostname`, `Icon`, `IsEnabled`, `SortOrder`, `Reachability`, `LastSeenUtc`, `LastLatencyMs`,
    `LastIpAddress`, `LastMacAddress`; behavior `Enable()`, `Disable()`, `MoveTo(int)`,
    `SetReachability(status, latencyMs, seenAtUtc)`,
    `UpdateDetails(...)` guarding invariants (non-empty name; hostname must match
    `^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)*$`; sort order ≥ 0).
    The hostname allowlist keeps the value to real host shapes (NetBIOS name, FQDN, IPv4 literal) because it
    is resolved by the probe and shown on the card — backslashes, spaces, underscores and relative segments
    must be rejected. `LastIpAddress`/`LastMacAddress` are only ever written by `SetReachability`, which
    ignores malformed values and leaves the previous hint in place.
- **Application** — orchestration + DTOs. `IAppRepository` + `IHealthHistoryRepository` + `IDockerService` ports,
  `AppService` implementing CRUD (+ `DockerContainer`) + `ProbeAsync` (records history) + `GetHealthHistoryAsync`,
  and `IHealthChecker`/`IDockerService` abstractions. No Postgres/Dapper references here.
  Machine side: `IMachineRepository` + `IMachineReachabilityProbe` ports and
  `MachineService` (CRUD, `RecordReachabilityAsync`).
  Logs side (read-only): `ILogRepository` port and `LogService` (`GetPagedAsync` with
  search/application/page/pageSize, `GetApplicationsAsync`, `GetByIdAsync`) over the
  `logs` table — no writes from the hub.
- **Infrastructure** — `DapperAppRepository` + `DapperHealthHistoryRepository` + `DapperMachineRepository`
  (raw SQL via Npgsql/Dapper),
  `SqlMigrationRunner` (executes `Migrations/Scripts/*.sql` in order, tracked in
  `schema_migrations`), `HttpHealthChecker` (async HTTP GET probe), `DockerService` (wraps `docker` CLI via `Process` — `ps -a --format "{{json .}}"`, `start/stop/restart`),
  `WindowsLanReachabilityProbe` (ICMP `Ping` + `ArpMacResolver` for the hardware address via
  `iphlpapi!SendARP`).
- **Api** — controllers (`AppsController`) return typed DTOs, map `404`/`400`/`409`,
  wire DI, serve `wwwroot`, top-level exception middleware returning RFC 7807 problems.

## 6. Database (Postgres, database `myhomelab`)

Managed by `scripts/create-db.ps1` (connects as `postgres` superuser). The hub uses
the URL **without** `/database` — a dev-instance URL. In practice the JSON schema
`public`):
- superuser password comes from env vars used on this box (`DB_*`), or a
  `MYHOMELAB_DB_PASSWORD` env var.

```sql
CREATE TABLE apps (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name              TEXT NOT NULL UNIQUE,
    description       TEXT NOT NULL DEFAULT '',
    url               TEXT NOT NULL,
    icon              TEXT NOT NULL DEFAULT 'web',      -- material symbol name
    category          TEXT NOT NULL DEFAULT 'other',
    port              INTEGER,                          -- known listening port (optional)
    tags              TEXT[] NOT NULL DEFAULT '{}',
    health_check_enabled  BOOLEAN NOT NULL DEFAULT TRUE,
    health_check_interval_ms INTEGER NOT NULL DEFAULT 30000,
    health_status     TEXT    NOT NULL DEFAULT 'unknown', -- unknown|up|down
    last_health_check TIMESTAMPTZ,
    last_latency_ms   INTEGER,
    is_enabled        BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order        INTEGER NOT NULL DEFAULT 0,
    created_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at        TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Health history (one row per probe, 0005_create_health_history.sql)
CREATE TABLE app_health_checks (
    id         BIGSERIAL PRIMARY KEY,
    app_id     UUID NOT NULL REFERENCES apps(id) ON DELETE CASCADE,
    status     TEXT NOT NULL CHECK (status IN ('unknown','up','down')),
    latency_ms INTEGER CHECK (latency_ms IS NULL OR latency_ms >= 0),
    checked_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX ix_app_health_checks_app_time ON app_health_checks (app_id, checked_at DESC);
CREATE INDEX ix_app_health_checks_checked_at ON app_health_checks (checked_at DESC);

-- Docker mapping (0006_add_docker_container.sql)
ALTER TABLE apps ADD COLUMN docker_container TEXT;
CREATE INDEX ix_apps_docker_container ON apps (docker_container) WHERE docker_container IS NOT NULL;

-- LAN Windows PCs (0008_create_lan_machines.sql)
CREATE TABLE lan_machines (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name             TEXT NOT NULL UNIQUE,
    description      TEXT NOT NULL DEFAULT '',
    hostname         TEXT NOT NULL,   -- NetBIOS / FQDN / IPv4; validated + CHECK-constrained
    icon             TEXT NOT NULL DEFAULT 'computer',
    is_enabled       BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order       INTEGER NOT NULL DEFAULT 0,
    reachability     TEXT NOT NULL DEFAULT 'unknown', -- unknown|online|offline
    last_seen        TIMESTAMPTZ,
    last_latency_ms  INTEGER,
    last_ip          TEXT,   -- resolved IPv4 of the machine (0009)
    last_mac         TEXT,   -- ARP hardware address, aa:bb:cc:dd:ee:ff (0009)
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_lan_machines_hostname CHECK (hostname ~ '^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)*$'),
    CONSTRAINT ck_lan_machines_reachability CHECK (reachability IN ('unknown', 'online', 'offline')),
    CONSTRAINT ck_lan_machines_last_ip  CHECK (last_ip IS NULL OR last_ip ~ '^[0-9]{1,3}(\.[0-9]{1,3}){3}$'),
    CONSTRAINT ck_lan_machines_last_mac CHECK (last_mac IS NULL OR last_mac ~ '^[0-9a-f]{2}(:[0-9a-f]{2}){5}$')
);
CREATE INDEX ix_lan_machines_is_enabled ON lan_machines (is_enabled);
CREATE INDEX ix_lan_machines_sort_order ON lan_machines (sort_order, name);

-- shutdown/restart from the hub was removed (0010_drop_machine_shutdown_delay.sql)
ALTER TABLE lan_machines DROP CONSTRAINT IF EXISTS ck_lan_machines_shutdown_delay_s;
ALTER TABLE lan_machines DROP COLUMN IF EXISTS shutdown_delay_s;

-- Append-only log entries (0011_create_logs.sql), surfaced read-only on /logs
CREATE TABLE logs (
    id          INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    application VARCHAR(255) NOT NULL,
    log         TEXT NOT NULL DEFAULT ''
);
CREATE INDEX ix_logs_application ON logs (application);
CREATE INDEX ix_logs_id_desc ON logs (id DESC);
```

`last_ip` / `last_mac` are written **only** by the reachability probe, never by the API.
Both are hints: an offline or unresolvable machine keeps whatever it last resolved rather
than being blanked, and a malformed value is dropped instead of failing the probe pass.

Migrations live in `src/MyHomeLab.Infrastructure/Migrations/` as `NNNN_description.sql`
and apply automatically on startup.

## 7. API Contract (versioned under `/api`)

All JSON, UTC timestamps, errors as RFC 7807 `application/problem+json`.

```
GET    /api/apps                -> AppSummary[]   (includes health, sortable/server-filterable)
GET    /api/apps/{id}           -> AppDetail
POST   /api/apps                -> 201 AppDetail  (409 on duplicate name)
PUT    /api/apps/{id}           -> 200 AppDetail  (full update)
PATCH  /api/apps/{id}           -> 200 AppDetail  (partial: status toggle, sort order)
DELETE /api/apps/{id}           -> 204
POST   /api/apps/{id}/check     -> 200 {status, latencyMs}   (manual probe now)
GET    /api/apps/{id}/history?hours=24&limit=200 -> HealthHistory {points, uptime}
GET    /api/apps/{id}/docker    -> DockerContainer (200) or 404 if no mapping / not found
POST   /api/apps/{id}/docker/{start|stop|restart} -> DockerActionResult
GET    /api/docker/containers   -> DockerContainer[]
GET    /api/docker/containers/{name} -> DockerContainer
POST   /api/docker/containers/{name}/{start|stop|restart} -> DockerActionResult
GET    /api/system              -> SystemMetrics   (live CPU/RAM/disks of this host)
GET    /api/system/network      -> NetworkSample[] (rolling ~5 min throughput history)
GET    /api/system/postgres     -> PostgresMetrics (live vitals of the hub's own database)
POST   /api/system/power        -> PowerResponse   (shutdown/reboot host, uses shutdown /s|/r /t 0 or shutdown -h|-r now)
GET    /api/healthz             -> 200 {status:"ok", version}
GET    /api/categories          -> string[]      (distinct categories, for the form)
GET    /api/terminal/config     -> TerminalConfig (shell allowlist, timeout, default cwd)
POST   /api/terminal/exec       -> TerminalResult (exec powershell/cmd in cwd, returns stdout+stderr)
GET    /api/logs?search=&application=&page=1&pageSize=50 -> PagedLogs {items, page, pageSize, totalCount, totalPages} (newest first, read-only)
GET    /api/logs/applications   -> string[] (distinct applications, for the filter)
GET    /api/logs/{id}           -> LogEntry
GET    /api/files?path=         -> FileListResponse {path, entries, quotaUsedBytes, quotaMaxBytes}
GET    /api/files/config        -> FileServerConfig {enabled, rootName, quotaMaxBytes}
GET    /api/files/download?path= -> file bytes (attachment, range supported for media seek)
POST   /api/files/upload?path=&overwrite= -> FileEntry[] (multipart form-data, streams to disk)
POST   /api/files/mkdir         -> FileEntry (body {parentPath, name})
POST   /api/files/rename        -> FileEntry (body {from, to} — rename or move)
DELETE /api/files?path=&recursive= -> 204 (recursive=true deletes non-empty folders)
GET    /swagger                 -> Swagger UI
```

`LogEntry`:
```json
{
  "id": 42,
  "application": "api",
  "log": "startup ok"
}
```
`PagedLogs`:
```json
{
  "items": [{ "id": 42, "application": "api", "log": "startup ok" }],
  "page": 1,
  "pageSize": 50,
  "totalCount": 1234,
  "totalPages": 25
}
```
`page` (< 1 falls back to 1), `pageSize` (1–100, default 50 — out-of-range values
fall back to the default). `search` matches `application` or `log`
(case-insensitive), `application` is an exact match. `totalCount`/`totalPages`
reflect the filtered set, so the UI can page through it.

`AppDetail`:
```json
{
  "id": "uuid",
  "name": "SearXNG",
  "description": "Meta search engine",
  "url": "http://192.168.15.22:8888",
  "icon": "travel_explore",
  "category": "tools",
  "port": 8888,
  "tags": ["search"],
  "dockerContainer": "searxng",
  "healthCheckEnabled": true,
  "healthCheckIntervalMs": 30000,
  "healthStatus": "up",
  "lastHealthCheckUtc": "2026-09-12T18:00:00Z",
  "lastLatencyMs": 45,
  "isEnabled": true,
  "sortOrder": 0,
  "createdAtUtc": "...",
  "updatedAtUtc": "..."
}
```
`dockerContainer` is `null` for native/remote apps; when set it enables Dashboard/Manage start/stop/restart. Suggested values for this lab: `pihole` (Pi-hole on :80), `open-webui` (:3000), `searxng` (:8888), `romm`/`romm_db`/`valkey` (:8082 via compose), `retrom` (:8083), `pcsx2`/`webstation` (RomM streaming).

`DockerContainer` / `DockerActionResult` (from `GET /api/docker/*` and `POST /api/apps/{id}/docker/*`):
```json
{
  "id": "a1b2c3...",
  "name": "searxng",
  "image": "searxng/searxng:latest",
  "state": "running",
  "status": "Up 2 hours",
  "ports": "0.0.0.0:8888->8080/tcp",
  "createdAt": "2026-09-20T12:00:00Z"
}
{
  "container": "searxng",
  "action": "restart",
  "success": true,
  "status": "Up 5 seconds",
  "state": "running",
  "message": "restart succeeded."
}
```
Exec path: `DockerService` validates `^[a-zA-Z0-9][a-zA-Z0-9_.\-]*$`, runs `docker ps -a --format "{{json .}}"` (list) or `docker {start|stop|restart} <name>` via `Process` with `Docker:TimeoutSeconds` (default 30s), re-inspects after. Errors map to RFC7807 400/404. Disabled via `Docker:Enabled=false`. Requires `docker` CLI on PATH of the service user (LocalSystem needs `C:\Program Files\Docker\Docker\resources\bin` on PATH — installer does this).

### Machines (`/api/machines`)

CRUD over `lan_machines` plus a background reachability probe. There is no power control:
the hub can only observe whether a machine answers, it never changes its power state.
(The header `Power` dropdown is a different, host-scoped feature — see `/api/system/power`.)

| Method | Route | Returns |
| --- | --- | --- |
| `GET` | `/api/machines?search=&enabledOnly=` | `200` `MachineSummary[]` |
| `GET` | `/api/machines/{id}` | `200` `MachineDetail` |
| `POST` | `/api/machines` | `201` `MachineDetail` |
| `PUT` | `/api/machines/{id}` | `200` `MachineDetail` |
| `PATCH` | `/api/machines/{id}` | `200` `MachineDetail` |
| `DELETE` | `/api/machines/{id}` | `204` |
| `GET` | `/api/machines/topology` | `200` `NetworkTopology` (gateway + hub NICs with link kind + all nodes) |
| `GET` | `/api/machines/discovered` | `200` `DiscoveryResult` (cached sightings incl. device type/icon hints) |
| `POST` | `/api/machines/discover` | `200` `DiscoveryResult` (full scan: ping-sweep + ARP + reverse DNS + SSDP) |

`MachineDetail`:
```json
{
  "id": "uuid",
  "name": "Gaming PC",
  "description": "Living room desktop",
  "hostname": "gaming-pc",
  "icon": "computer",
  "reachability": "online",
  "lastSeenUtc": "2026-09-26T18:00:00Z",
  "lastLatencyMs": 3,
  "ipAddress": "192.168.15.9",
  "macAddress": "f4:b5:20:5f:0a:55",
  "isEnabled": true,
  "sortOrder": 0,
  "createdAtUtc": "...",
  "updatedAtUtc": "..."
}
```

`MachineSummary` is the same shape minus `lastSeenUtc`/`createdAtUtc`/`updatedAtUtc`. A
duplicate name (case-insensitively) maps to RFC7807 `409`, a missing id to `404`, and a
hostname outside the allowlist to `400`.

Probe path: every `Machines:ProbeIntervalSeconds` the background service pings each enabled
machine and writes back `reachability`, `last_seen`, `last_latency_ms`, `last_ip` and
`last_mac`. The address is resolved as **IPv4 only** — a dual-stack host usually answers the
echo over IPv6, and a link-local v6 address is useless as a label and cannot be resolved to a
hardware address, so the probe takes the host's IPv4 sibling instead. `last_mac` therefore
stays null for hosts that are reachable only off-subnet, and both fields keep their previous
value when a pass resolves nothing. The card shows the hostname, plus the IP and MAC when
they are known and the IP differs from the configured hostname.

`HealthHistory` (from `GET /api/apps/{id}/history`) — aggregates the `app_health_checks` table:
```json
{
  "points": [
    { "status": "up", "latencyMs": 42, "checkedAtUtc": "2026-09-20T18:00:00Z" },
    { "status": "down", "latencyMs": null, "checkedAtUtc": "2026-09-20T18:01:00Z" }
  ],
  "uptime": {
    "uptimePercent": 98.5,
    "totalChecks": 200,
    "upCount": 197,
    "downCount": 3,
    "averageLatencyMs": 44.2,
    "minLatencyMs": 12,
    "maxLatencyMs": 210
  }
}
```
Query params: `hours` (1–720, default 24) clamps the `sinceUtc` window, `limit` (1–1000, default 200) caps rows — ordered `checked_at ASC` for charting. Missing history returns empty `points` with zeroed uptime (graceful before first probe).

`SystemMetrics` (from `GET /api/system`) — CPU/RAM read via Windows
`GetSystemTimes`/`GlobalMemoryStatusEx` (P/Invoke), disks via `DriveInfo`:
```json
{
  "hostName": "SERVER",
  "operatingSystem": "Microsoft Windows 10.0.x",
  "architecture": "X64",
  "runtimeVersion": ".NET 10.0.x",
  "processorCount": 8,
  "cpuUsagePercent": 12.4,
  "totalMemoryBytes": 34359738368,
  "availableMemoryBytes": 22004811776,
  "usedMemoryBytes": 12354926592,
  "memoryUsagePercent": 36.0,
  "uptimeSeconds": 604800,
  "sampledAtUtc": "2026-09-12T12:34:56Z",
  "disks": [
    {
      "name": "C:\\",
      "driveType": "Fixed",
      "fileSystem": "NTFS",
      "totalBytes": 500107862016,
      "availableBytes": 151072291840,
      "usedBytes": 349035570176,
      "usagePercent": 69.8
    }
  ]
}
```

CPU and board temperatures are read by `SystemSensorService` (singleton) through
LibreHardwareMonitor. LHM 0.9.5+ replaced the old WinRing0 driver with **PawnIO**, and its
low-level access to the CPU thermal sensors now requires the PawnIO kernel driver to be
installed: without it every CPU/motherboard temperature sensor enumerates but stays `null`
(so the System panel shows no CPU temp). Install it once per host with
`scripts/install-pawnio.ps1` (elevated); it also restarts the service. `SystemSensorService`
logs a warning at startup when PawnIO is missing.

`NetworkSample` (from `GET /api/system/network`) — rolling throughput history, newest last.
Produced by `NetworkThroughputService` (singleton, `Api/Services`): it samples total
bytes sent/received across up, non-loopback interfaces via the managed
`NetworkInterface.GetAllNetworkInterfaces()` API (works on Windows and Linux), sums the
deltas over each ~4s+ window into a ring buffer (90 points ≈ several minutes), and hands
the buffer to the client for graphing:
```json
[
  { "sampledAtUtc": "2026-09-19T18:00:05Z", "downloadBytesPerSec": 2048512.5, "uploadBytesPerSec": 131072.0 }
]
```

`PostgresMetrics` (from `GET /api/system/postgres`) — live vitals of the hub's own database.
`DapperPostgresMetricsReader` (Infrastructure) reads the cumulative `pg_stat_*` counters for
`current_database()` in a single round trip through the shared `NpgsqlDataSource`, and
`PostgresMetricsService` (singleton, `Api/Services`) differences them against the previous read:

| Field | Source |
| --- | --- |
| `version`, `uptimeSeconds` | `current_setting('server_version')`, `pg_postmaster_start_time()` |
| `sizeBytes` | `pg_database_size(current_database())` |
| `connectionsUsed` / `connectionsMax` / `connectionsActive` / `connectionsIdle` | `pg_stat_activity` + `max_connections` |
| `lockWaiters` | `pg_stat_activity` where `wait_event_type = 'Lock'` |
| `longestQuerySeconds` | oldest active query on this database |
| `transactionsPerSecond` | Δ(`xact_commit` + `xact_rollback`) |
| `transactionsTotal`, `rollbacksTotal`, `deadlocks` | `pg_stat_database` |
| `cacheHitRatio` | `blks_hit / (blks_read + blks_hit)`, cumulative |
| `tempBytesPerSecond`, `walBytesPerSecond` | Δ`temp_bytes`, Δ`pg_stat_wal.wal_bytes` |
| `checkpointsTotal` | `pg_stat_checkpointer.num_done` |

Two details worth knowing:

- **Rates need two samples.** The `pg_stat_*` views are monotonic counters, so anything
  rate-shaped is a difference between consecutive reads and reads `0` for the first ~5s after
  the hub starts. A counter that moves *backwards* (Postgres restarted, or `pg_stat_reset()`)
  invalidates the whole window rather than reporting a spike — same rebaseline behaviour as
  `NetworkThroughputService`.
- **Requires PostgreSQL 17+.** Checkpoints moved out of `pg_stat_bgwriter` into
  `pg_stat_checkpointer` in PG 17; on an older server that one scalar subquery fails, the
  endpoint returns a problem+json and the panel shows the group as unavailable. Everything else
  in the query is portable back to PG 9.6.

The hub connects as the bootstrap superuser, so every view is readable with no extra grants.

## 8. Health Checking

- `HttpHealthChecker` issues an async GET against `url` with a short timeout,
  treats any reachable response as `up`, timeouts/`HttpRequestException` as `down`.
- Runs in `Api` as a hosted `BackgroundService` (`HealthCheckBackgroundService`) every 5s tick,
  checking due apps where `is_enabled && health_check_enabled` and `now - lastRun >= health_check_interval_ms`.
- Each probe writes back `health_status`, `last_latency_ms`, `last_health_check` to `apps`
  **and** inserts one row into `app_health_checks` (`DapperHealthHistoryRepository.AddAsync`)
  via `AppService.ProbeAsync` (history is best-effort — failure does not roll back the `apps` update).
  `ON DELETE CASCADE` cleans history when an app is removed.
- `GET /api/apps/{id}/history` aggregates that table into `HealthHistory` (points + uptime %).
  Hours/limit clamping lives in `AppService.GetHealthHistoryAsync`.
- The frontend polls `GET /api/apps` every ~15s and `GET /api/apps/{id}/history` every ~30s per visible tile.

Machine reachability works the same way, on a separate loop: `MachineReachabilityService`
(hosted `BackgroundService` in `Api/Services`) ticks every `Machines:ProbeIntervalSeconds`
and, for each machine with `is_enabled`, calls `IMachineReachabilityProbe` (ICMP `Ping`
in Infrastructure) and writes `reachability`, `last_seen` and `last_latency_ms` back via
`MachineService.RecordReachabilityAsync`. Like `SetHealth`, `SetReachability` deliberately
does not touch `updated_at` so a 30s probe never looks like a user edit.

## 9. Frontend

- Dark, modern dashboard. Tailwind v4 with a CSS-variable theme; one accent color,
  rounded tiles, subtle shadows, right-aligned status dots.
- **Dashboard**: responsive flat tile grid (no category grouping). Tile = icon + name +
  one-click open (external tab) + status dot + tag chips + health strip. Search box filters by name/tag.
  Each tile (`frontend/src/components/AppCard.tsx:1`) shows a compact `HealthStrip` (`HealthStrip.tsx:1`) — last 30 probes as colored bars (up=green, down=red) with 24h uptime pill — and a toggle (`monitoring` icon) that expands an inline `HealthHistoryPanel` (`HealthHistoryPanel.tsx:1`): hour selector (1h/6h/24h/7d), uptime/avg latency/checks stats, hand-rolled SVG latency line + down markers, and a dense availability bar. Strip and panel poll `GET /api/apps/{id}/history` every 30s via `useHealthHistory` (`features/apps/useHealthHistory.ts:1`) with centralized `queryKeys.appHistory`.
  When `dockerContainer` is set, the tile also renders `DockerControls` (`components/DockerControls.tsx:1`) — container name + `state` pill (running=green pulse) + `Start`/`Stop`/`Restart` (confirm for stop) powered by `GET /api/apps/{id}/docker` + `POST /api/apps/{id}/docker/{action}` via `features/docker/useDocker.ts:1` (`queryKeys.appDocker`/`dockerContainers`, 15s poll).
  A sticky **System panel** sits beside the grid (≥xl): live CPU ring, memory and
  per-disk bars (orange accent), hostname, OS, uptime — polled every 5s via `/api/system`.
  The `Ring`/`Bar` primitives it uses were extracted to `components/Metric.tsx:1` so the
  Postgres page can share them.
  A **Network graph** card hangs below it: hand-rolled SVG area/line chart (no chart
  library) of download (accent) vs upload (sky blue) throughput over a rolling window,
  with smooth curves, hover crosshair + tooltip, and live bytes/sec readout. Polled every
  5s via `/api/system/network`. (Upload color token `--color-upload` lives in `index.css`.)
  Content is centered at `max-w-7xl`.
- **Manage**: CRUD table with inline create, edit drawer, delete confirm, sort order.
  Category dropdown (from `/api/categories`), Material Symbol picker, tags input.
  New `Docker container` column shows mapped name + live state pill from `GET /api/docker/containers` (`useDockerContainers`). Actions per row now include docker `Start`/`Stop`/`Restart` (disabled per state, busy spinner) alongside `Probe now`. Create/Edit drawer (`AppForm.tsx:1`) adds a `Docker container` field with a `<datalist>` of live containers (suggestions) — empty means non-docker.
- **Machines**: LAN Windows PC registry (`/machines`). Card grid (`components/MachineCard.tsx:1`) —
  icon tile, name, description, `lan` hostname chip (plus the resolved IPv4 when it differs and the
  ARP MAC when known), reachability dot (reuses `StatusDot` by mapping
  `online→up` / `offline→down` / `unknown→unknown`), ICMP latency in the footer. Footer also has
  show/hide, edit and delete. Reachability, latency and addresses come
  from `GET /api/machines` polled every 15s. Create/Edit drawer (`components/MachineForm.tsx:1`) mirrors `AppForm`:
  name, hostname (with the allowlist rule surfaced as help text), description, icon, sort order,
  plus a static requirements note. Accepts an `initial` prefill for one-click adds from discovery.
- **Network map** (Map/Grid toggle on `/machines`, default Map): hand-rolled SVG hierarchy
  (`components/NetworkMap.tsx:1`, no chart library) — router node on top (default gateway from
  each NIC's `GatewayAddresses`, MAC from the ARP table, hostname via reverse DNS), hub below it,
  registered machines in rows beneath with status-colored links and latency tooltips, discovered
  unknowns in a dashed outer section. Layout is dynamic: ring rows and viewBox height grow with
  node counts (fixed viewBoxes clipped labels), labels truncate with full-name tooltips, nodes
  render their real Material Symbol glyph. Click-to-select shows a detail bar (edit/hide/delete
  reuse the grid handlers). Data from `GET /api/machines/topology` (`NetworkTopology`: gateway +
  hub NICs enumerated via `NetworkInterface`, same API the throughput sampler uses) polled every
  15s via `useTopology` (`queryKeys.topology`).
- **Cable vs Wi-Fi (honest limits)**: each hub NIC reports its link kind (`Wireless80211` →
  wireless, Ethernet family → wired, else other), shown as `wifi`/`lan` badges under the hub with
  per-NIC tooltips. Remote devices' medium is **not shown** — no LAN API exposes another host's
  link medium; the wireless association table lives inside the router. Device links stay neutral
  rather than guessing.
- **LAN discovery** ("Discovered on LAN" section on `/machines`): transient devices that need no
  fixed IP or hostname. `LanDiscoveryService` (Infrastructure, singleton) ping-sweeps each local
  /24 (bounded concurrency, `DiscoveryTimeoutMs`), then harvests `GetIpNetTable` from `iphlpapi`
  (same P/Invoke style as `ArpMacResolver`) — so ARP-but-no-ping devices still show up — with
  best-effort reverse DNS and registry matching by IP/MAC (`DiscoveredDeviceMatcher`).
  `POST /api/machines/discover` runs the full scan (adds `SsdpProbe`: UDP M-SEARCH multicast,
  ~3s listen, follows `LOCATION` URLs for `friendlyName`/`modelName` — how TVs/consoles/NAS get
  named), `GET /api/machines/discovered` serves the cache, and `LanDiscoveryBackgroundService`
  re-scans ping+ARP only every `DiscoveryIntervalSeconds` (all gated by
  `Machines:DiscoveryEnabled`). Device typing (`DeviceTypeGuesser`, precedence SSDP model >
  hostname keywords > OUI vendor name; `OuiHints` is a curated ~60-entry MAC-prefix table that
  contributes a vendor name only, since vendors make many device kinds) yields `deviceType` +
  `suggestedIcon` shown on map nodes and list rows. Sightings live in memory (`SightingsCache`:
  first-seen sticks, unseen past `DiscoveryExpiryMinutes` drops off) — no migration, registry
  untouched. Frontend polls via `useDiscovered` (`queryKeys.discovered`) with a `useScanNetwork`
  mutation behind the Scan now button; each unknown row has one-click **Add** prefilling
  `MachineForm` (IP as hostname — always allowlist-valid — reverse-DNS name or IP as the name
  suggestion, suggested icon pre-selected but overridable). An empty scan shows the ProtonVPN
  hint, since the VPN blocks all LAN traffic (see below).
- **Postgres** (`/postgres`): dedicated page (`pages/Postgres.tsx:1`) for the hub's own database —
  header with version, database name, server uptime and size, then two `Ring` tiles
  (connections used/max %, cache-hit %) over a grid of `Tile`s: throughput, WAL rate, temp-spill
  rate, checkpoints, transactions + rollbacks, deadlocks, longest query, sessions waiting on a
  lock. A "Needs attention" block appears only when lock waits >0, a query has run >30s, or
  deadlocks >0. Polls `GET /api/system/postgres` every 5s via `usePostgresMetrics`
  (`queryKeys.systemPostgres`). Lives on its own page rather than in the dashboard System panel
  because the metric set is too tall for the ~380px sidebar column.
- **Logs** (`/logs`): read-only viewer over the `logs` table (`pages/Logs.tsx:1`). Search
  box (debounced, matches application or text) + application filter dropdown (distinct
  values from `GET /api/logs/applications` via `useLogApplications`,
  `queryKeys.logApplications`), newest-first list with one-line previews and click-to-expand full text
  (`<pre>`, scrollable). Server-side pagination via `GET /api/logs` (`page`/`pageSize`,
  `LogPage` shape) polled every 15s via `useLogs` (`features/logs/useLogs.ts:1`,
  `queryKeys.logs`, previous page kept while fetching); shared `Pagination`
  (`components/Pagination.tsx:1`, page-number math in `lib/pagination.ts:1`) with
  rows-per-page selector (25/50/100) and "Showing X–Y of Z". Filters and page size
  reset to page 1. Single `LogDto` shape serves as both summary and detail —
  no create/edit/delete.
  The hub logs its own failures there too: `PostgresLogSink` (`Infrastructure/Logging`)
  persists every Serilog `Error`/`Fatal` (unhandled 500s, terminal/power failures, probe
  pass failures, fatal migration failure at startup) with `application='myhomelab'`.
  `Warning`/`Information` stay on console/file only to bound table growth. The sink buffers
  into a bounded channel (1000 entries, drops newest when full) and flushes one batched
  `INSERT` every 2s or 50 rows; `Dispose` (via Serilog's shutdown flush) drains the queue
  with a 10s bound. Each write has a 5s timeout and failures are swallowed with a `SelfLog`
  note, so a down database can never break the path reporting the error — console/file
  remain the durable backstop.
- **Terminal**: full-width command prompt tab (`/terminal`). Dark terminal chrome (window
  dots, monospace output, `cwd>` prompt), shell selector (powershell/pwsh/cmd), per-tab
  `cwd` that persists across `cd` commands, history navigation (↑/↓), `clear`/`help` built-ins,
  stdout+stderr combined + truncation at `MaxOutputBytes`, timeout badge and duration. Talks to
  `POST /api/terminal/exec` via TanStack Query mutation; `GET /api/terminal/config` for
  allowlist/timeout defaults. Follows same `fetch` client + `lib/queryKeys` pattern.
- **Files** (`/files`): web cloud over `C:\Shared-Server` (`FileServer` config: single jailed root, 25 GB
  directory quota, no per-file cap). Breadcrumb + quota bar, search filter, drag-drop/multi upload with
  progress (409 → overwrite confirm), download (range-enabled so video seeks), mkdir, rename/move,
  delete (recursive confirm for non-empty folders), inline image preview and video via
  Plyr (`components/VideoPlayer.tsx:1`, lazy-loaded so it only downloads on first video
  preview; Plyr ships no types, covered by a minimal `lib/plyr.d.ts`). Backend
  (`FilesController` + `FileSystemFileService`) jails every path with `Path.GetFullPath` prefix checks,
  streams uploads/downloads without buffering, enforces quota before/during writes (partial removed).
  `desktop.ini`/`Thumbs.db` hidden server-side. Frontend: `api/files.ts` (XHR for progress) +
  `features/files/useFiles.ts` (`queryKeys.files*`) + `pages/Files.tsx:1`.
- **Header power control**: `Layout` nav shows `Dashboard | Manage | Machines | Files | Logs | Postgres | Terminal | Power` (`frontend/src/components/Layout.tsx:35`) — red `power_settings_new` dropdown beside Terminal with `Shut down` and `Restart` (`restart_alt` accent). Calls `POST /api/system/power {action:"shutdown"|"reboot"}` (`src/MyHomeLab.Api/Controllers/SystemController.cs:35`, `src/MyHomeLab.Infrastructure/System/PowerService.cs:1`, `src/MyHomeLab.Application/Abstractions/IPowerService.cs:1`) via TanStack Query mutation with `ConfirmDialog` (shutdown `/s /t 0` vs `/r /t 0` on Windows, `-h now` vs `-r now` on Linux). WoL is handled separately via `scripts/enable-wol.ps1` (runs `powercfg /h off`, `Set-NetAdapterPowerManagement`, `HiberbootEnabled=0` — BIOS still needs `ErP Disabled`, use `shutdown /r /fw /t 0` to enter). This dropdown controls the **hub host**; the Machines tab only observes the **remote PCs**.
- **Settings**: placeholder page for later (theme, refresh interval, header text).
- Single API base from same origin when served on 443; in dev, Vite proxy
  `/api → http://192.168.15.22:8080`.

**Auth note**: LAN-trusted + self-signed TLS for v1. A simple access token
(shared secret header) is the planned v1.1 hardening — out of scope now.

## 10. Configuration

`src/MyHomeLab.Api/appsettings.json`:
```jsonc
{
  "Kestrel": {
    "Endpoints": {
      "Https443": { "Url": "https://0.0.0.0:443" },
      "Http8080": { "Url": "http://0.0.0.0:8080" }
    }
  },
  "ConnectionStrings": {
    "MyHomeLab": "Host=localhost;Port=5432;Database=myhomelab;Username=postgres;Password=..."
  },
  "Health": { "TimeoutSeconds": 3, "DefaultIntervalMs": 30000 },
  "Terminal": {
    "Enabled": true,
    "DefaultShell": "powershell",
    "AllowedShells": ["powershell", "pwsh", "cmd"],
    "TimeoutSeconds": 30,
    "MaxOutputBytes": 100000
  },
  "Docker": {
    "Enabled": true,
    "TimeoutSeconds": 30,
    "ExecutablePath": "docker"
  },
  "Machines": {
    "Enabled": true,
    "ProbeIntervalSeconds": 30,
    "ProbeTimeoutMs": 2000,
    "DiscoveryEnabled": true,
    "DiscoveryIntervalSeconds": 300,
    "DiscoveryTimeoutMs": 400,
    "DiscoveryExpiryMinutes": 15
  },
  "FileServer": {
    "Enabled": true,
    "RootPath": "C:\\Shared-Server",
    "QuotaMaxBytes": 26843545600,
    "HideSystemFiles": true
  }
}
```
Secrets never committed: connection string overridable via
`ConnectionStrings__MyHomeLab` env var; cert via `ASPNETCORE_Kestrel__Certificates__Default__Path`.

### Machines: operational requirements

The Machines feature is read-only with respect to the target, so it needs no credentials
and no Windows rights on the other machines. It does need:

- The hostname to resolve **on the hub**. A NetBIOS name needs a working WINS/LAN resolution
  path; a FQDN or an IPv4 literal always works.
- **ICMP echo request** allowed towards the target. A blocked ping shows `unknown` and a
  `null` latency — the machine is not reported as offline, because the probe cannot tell
  "off" from "filtered".
- Same-subnet addressing for `last_mac`: `ArpMacResolver` uses `SendARP`, which only answers
  for IPv4 addresses on a directly attached network. Off-subnet targets still report
  `online` and their IP, with `macAddress` left `null`.
- `Machines:Enabled=false` stops the background probe entirely; machines are then never
  updated and keep whatever reachability they last had.

#### Known host interaction: ProtonVPN blocks all LAN traffic

When the ProtonVPN client is connected on the hub, **every** machine in the LAN shows
`unknown` with a `null` latency. This is not a probe or firewall bug — verified on this host:

- `ProtonVPNCallout` (`ProtonVPN/ProtonVPN.CalloutDriver.sys`, a WFP callout driver) drops
  outbound packets that do not go through the `ProTUN` tunnel, so all `192.168.15.0/24`
  traffic is killed. `ping.exe` reports `General failure` (IP status `11050`,
  `UnrecognizedNextHeader`), which is a *local send* error rather than a timeout.
- `Get-NetFirewallRule -Direction Outbound -Action Block` returns **0** rules, so the block is
  invisible to firewall rules. `Get-CimInstance Win32_SystemDriver` is what surfaces it.
- ARP still resolves (`Get-NetNeighbor` shows `192.168.15.9` as `Reachable`) and the on-link
  `192.168.15.0/24` route is correct, so the network itself is healthy — only IP is blocked.

Fix: disconnect ProtonVPN (or enable its "Allow LAN traffic" / disable NetShield's local-network
blocking). The hub needs `Core Networking Diagnostics - ICMP Echo Request (ICMPv4-Out)` **enabled**
on the Wi-Fi profile for the probe to work when the VPN is off; that rule ships disabled on this
host and outbound ping fails without it.


## 11. Build / Run / Deploy

```
# Database (once)
scripts/create-db.ps1                    # create DB + grants

# Dev
scripts/dev.ps1                          # watch API on 8080 + Vite dev on 5173 (proxy /api)

# Production build
scripts/build.ps1                        # npm build -> frontend/dist -> API wwwroot, dotnet publish

# Run / install as a service (survives reboot)
scripts/install-service.ps1              # sc create / Windows equivalent (run as LocalSystem)
scripts/install-pawnio.ps1               # install PawnIO driver (required for CPU temperatures)
# then: plc <app> start (or `Start-Service MyHomeLab`)
```

Output single deployable: `MyHomeLab.Api` serves everything on :443/:8080.

## 12. Acceptance Criteria

- Browsing `https://<host>` shows the dark dashboard with seeded app tiles.
- Health dots go up/down as services are stopped/started.
- CRUD round-trips persist to Postgres; delete is immediate; duplicate name → 409.
- `http://<host>:8080/api/apps` returns JSON (API reachable on its own port).
- Reboot-safe: after Windows restart, hub is running (service) and DB intact.
- `dotnet build` and `npm run build` clean; no warnings introduced.