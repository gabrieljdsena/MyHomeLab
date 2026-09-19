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
  - properties: `Name`, `Description`, `Url`, `Icon`, `Category`, `Port`, `Tags`,
    `HealthCheckEnabled`, `HealthCheckIntervalMs`, `IsEnabled`, `SortOrder`
  - behavior (not anemic): `Enable()`, `Disable()`, `SetHealth(status, latencyMs)`,
    `UpdateDetails(...)` guard invariants (non-empty name, valid absolute URL)
  - value objects: `AppId`, `AppHealthStatus` (`Unknown | Up | Down`), `Category`
- **Application** — orchestration + DTOs. `IAppRepository` (the only port),
  `AppService` implementing CRUD, and an `IHealthChecker` abstraction.
  No Postgres/Dapper references here.
- **Infrastructure** — `DapperAppRepository` (raw SQL via Npgsql/Dapper),
  `SqlMigrationRunner` (executes `Migrations/*.sql` in order, tracked in
  `schema_migrations`), `HttpHealthChecker` (async HTTP GET probe).
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
GET    /api/system              -> SystemMetrics   (live CPU/RAM/disks of this host)
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
  "name": "Jellyfin",
  "description": "Media server",
  "url": "http://192.168.15.22:8096",
  "icon": "movie",
  "category": "media",
  "port": 8096,
  "tags": ["media", "video"],
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

## 8. Health Checking

- `HttpHealthChecker` issues an async HEAD/GET against `url` with a short timeout,
  treats 2xx/3xx + 5xx-with-body as reachable (home services vary).
- Runs in `Api` as a hosted `BackgroundService` every `health_check_interval_ms`
  per enabled app with `health_check_enabled = true`.
- Writes back only `health_status`, `last_latency_ms`, `last_health_check` — one row
  update, no history table yet.
- The frontend polls `GET /api/apps` every ~15s (TanStack Query refetchInterval).

## 9. Frontend

- Dark, modern dashboard. Tailwind v4 with a CSS-variable theme; one accent color,
  rounded tiles, subtle shadows, right-aligned status dots.
- **Dashboard**: responsive flat tile grid (no category grouping). Tile = icon + name +
  one-click open (external tab) + status dot + tag chips. Search box filters by name/tag.
  A sticky **System panel** sits beside the grid (≥xl): live CPU ring, memory and
  per-disk bars (orange accent), hostname, OS, uptime — polled every 5s via `/api/system`.
  Content is centered at `max-w-7xl`.
- **Manage**: CRUD table with inline create, edit drawer, delete confirm, sort order.
  Category dropdown (from `/api/categories`), Material Symbol picker, tags input.
  Live "probe now" button per row.
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