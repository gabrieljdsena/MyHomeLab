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
- Serilog (console + file) — structured logging
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
    │   ├── pages/             # Dashboard, Manage, Settings
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
- **Application** — orchestration + DTOs. `IAppRepository` + `IHealthHistoryRepository` + `IDockerService` ports,
  `AppService` implementing CRUD (+ `DockerContainer`) + `ProbeAsync` (records history) + `GetHealthHistoryAsync`,
  and `IHealthChecker`/`IDockerService` abstractions. No Postgres/Dapper references here.
- **Infrastructure** — `DapperAppRepository` + `DapperHealthHistoryRepository` (raw SQL via Npgsql/Dapper),
  `SqlMigrationRunner` (executes `Migrations/*.sql` in order, tracked in
  `schema_migrations`), `HttpHealthChecker` (async HTTP GET probe), `DockerService` (wraps `docker` CLI via `Process` — `ps -a --format "{{json .}}"`, `start/stop/restart`).
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
```

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
POST   /api/system/power        -> PowerResponse   (shutdown/reboot host, uses shutdown /s|/r /t 0 or shutdown -h|-r now)
GET    /api/healthz             -> 200 {status:"ok", version}
GET    /api/categories          -> string[]      (distinct categories, for the form)
GET    /api/terminal/config     -> TerminalConfig (shell allowlist, timeout, default cwd)
POST   /api/terminal/exec       -> TerminalResult (exec powershell/cmd in cwd, returns stdout+stderr)
GET    /swagger                 -> Swagger UI
```

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

## 9. Frontend

- Dark, modern dashboard. Tailwind v4 with a CSS-variable theme; one accent color,
  rounded tiles, subtle shadows, right-aligned status dots.
- **Dashboard**: responsive flat tile grid (no category grouping). Tile = icon + name +
  one-click open (external tab) + status dot + tag chips + health strip. Search box filters by name/tag.
  Each tile (`frontend/src/components/AppCard.tsx:1`) shows a compact `HealthStrip` (`HealthStrip.tsx:1`) — last 30 probes as colored bars (up=green, down=red) with 24h uptime pill — and a toggle (`monitoring` icon) that expands an inline `HealthHistoryPanel` (`HealthHistoryPanel.tsx:1`): hour selector (1h/6h/24h/7d), uptime/avg latency/checks stats, hand-rolled SVG latency line + down markers, and a dense availability bar. Strip and panel poll `GET /api/apps/{id}/history` every 30s via `useHealthHistory` (`features/apps/useHealthHistory.ts:1`) with centralized `queryKeys.appHistory`.
  When `dockerContainer` is set, the tile also renders `DockerControls` (`components/DockerControls.tsx:1`) — container name + `state` pill (running=green pulse) + `Start`/`Stop`/`Restart` (confirm for stop) powered by `GET /api/apps/{id}/docker` + `POST /api/apps/{id}/docker/{action}` via `features/docker/useDocker.ts:1` (`queryKeys.appDocker`/`dockerContainers`, 15s poll).
  A sticky **System panel** sits beside the grid (≥xl): live CPU ring, memory and
  per-disk bars (orange accent), hostname, OS, uptime — polled every 5s via `/api/system`.
  A **Network graph** card hangs below it: hand-rolled SVG area/line chart (no chart
  library) of download (accent) vs upload (sky blue) throughput over a rolling window,
  with smooth curves, hover crosshair + tooltip, and live bytes/sec readout. Polled every
  5s via `/api/system/network`. (Upload color token `--color-upload` lives in `index.css`.)
  Content is centered at `max-w-7xl`.
- **Manage**: CRUD table with inline create, edit drawer, delete confirm, sort order.
  Category dropdown (from `/api/categories`), Material Symbol picker, tags input.
  New `Docker container` column shows mapped name + live state pill from `GET /api/docker/containers` (`useDockerContainers`). Actions per row now include docker `Start`/`Stop`/`Restart` (disabled per state, busy spinner) alongside `Probe now`. Create/Edit drawer (`AppForm.tsx:1`) adds a `Docker container` field with a `<datalist>` of live containers (suggestions) — empty means non-docker.
- **Terminal**: full-width command prompt tab (`/terminal`). Dark terminal chrome (window
  dots, monospace output, `cwd>` prompt), shell selector (powershell/pwsh/cmd), per-tab
  `cwd` that persists across `cd` commands, history navigation (↑/↓), `clear`/`help` built-ins,
  stdout+stderr combined + truncation at `MaxOutputBytes`, timeout badge and duration. Talks to
  `POST /api/terminal/exec` via TanStack Query mutation; `GET /api/terminal/config` for
  allowlist/timeout defaults. Follows same `fetch` client + `lib/queryKeys` pattern.
- **Header power control**: `Layout` nav shows `Dashboard | Manage | Terminal | Power` (`frontend/src/components/Layout.tsx:35`) — red `power_settings_new` dropdown beside Terminal with `Shut down` and `Restart` (`restart_alt` accent). Calls `POST /api/system/power {action:"shutdown"|"reboot"}` (`src/MyHomeLab.Api/Controllers/SystemController.cs:35`, `src/MyHomeLab.Infrastructure/System/PowerService.cs:1`, `src/MyHomeLab.Application/Abstractions/IPowerService.cs:1`) via TanStack Query mutation with `ConfirmDialog` (shutdown `/s /t 0` vs `/r /t 0` on Windows, `-h now` vs `-r now` on Linux). WoL is handled separately via `scripts/enable-wol.ps1` (runs `powercfg /h off`, `Set-NetAdapterPowerManagement`, `HiberbootEnabled=0` — BIOS still needs `ErP Disabled`, use `shutdown /r /fw /t 0` to enter).
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
  }
}
```
Secrets never committed: connection string overridable via
`ConnectionStrings__MyHomeLab` env var; cert via `ASPNETCORE_Kestrel__Certificates__Default__Path`.

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