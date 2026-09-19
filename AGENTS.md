# AGENTS.md — MyHomeLab Codebase Rules

Self-hosted hub (React + Tailwind frontend, ASP.NET Core REST API, DDD + Dapper,
Postgres). See `docs/implementation.md` for the full architecture. Read it when in doubt.

---

## 1. Layout & Layering

```
myhomelab/
├── scripts/                 # powershell: dev, build, create-db, install-service
├── src/MyHomeLab.Domain/          # entities/value objects — ZERO dependencies
├── src/MyHomeLab.Application/     # DTOs, services, IAppRepository port — no Infra refs
├── src/MyHomeLab.Infrastructure/  # Dapper repo, Npgsql, SQL migrations, health probe
├── src/MyHomeLab.Api/             # controllers, Kestrel config, static SPA hosting
└── frontend/                       # Vite + React + TS + Tailwind v4
```

Dependency rule: **Api → Application → Domain**; Infrastructure implements
Application ports only. Never reference Infrastructure or Npgsql from Api wiring
beyond DI composition. Never let Domain depend on any framework.

## 2. Commands

Backend (SDK: .NET 10, solution is `MyHomeLab.slnx`):
- `dotnet build` — build solution
- `dotnet test` — run xUnit tests (create them next to units under test)
- `dotnet run --project src/MyHomeLab.Api` — dev (binds :443 https + :8080 http)

Frontend (Node 22):
- `cd frontend; npm run dev` — Vite dev server (proxies `/api` → `http://192.168.15.22:8080`)
- `npm run build` — outputs to `frontend/dist`; `src/MyHomeLab.Api` copies it into `wwwroot` on build
- `npm run lint` — `oxlint` must pass
- `npm run typecheck` (`tsc -b`) — required before any frontend change is done

Full prod path: `scripts/build.ps1`, dev helper: `scripts/dev.ps1`.
DB bootstrap: `scripts/create-db.ps1`.

After ANY backend change: run `dotnet build`. After ANY frontend change: run
`npm run lint` and `npm run typecheck`. Fix everything you introduce.

## 3. Backend Conventions

- **Dapper + raw SQL only.** No EF Core. No MediatR, no AutoMapper, no DI-in-Domain.
- **DDD, not CRUD-thin**: model an `App` aggregate with real behavior (`Enable()`,
  `Disable()`, `SetHealth(...)`, `UpdateDetails(...)`). Guard invariants. Return
  meaningful domain errors mapped to HTTP (400/404/409).
- **Async everywhere**: `async Task<...>`, `NpgsqlConnection` opened per operation.
  Parameterized queries only — never string-interpolated SQL.
- **Types**: `AppId` is a UUID value object. `health_status` is `unknown|up|down`.
  Timestamps are `TIMESTAMPTZ` (UTC).
- HTTP errors are RFC 7807 `application/problem+json` from one exception handler.
- Migration files: `src/MyHomeLab.Infrastructure/Migrations/NNNN_description.sql`,
  applied in order on startup, recorded in `schema_migrations`. Never edit an
  applied migration — add a new one.
- Package versions: pin exact versions in `.csproj` / `global.json` style; no floats.
- No comments unless they explain *why* (non-obvious). Keep method bodies small.

## 4. Frontend Conventions

- Vite + React 19 + TypeScript + Tailwind CSS v4 + React Router + TanStack Query v5.
- All server state through TanStack Query with centralized query keys in `lib/`.
- Thin `fetch` client in `api/`; every call typed with the DTO shape; no `any`.
- Components in `components/`, feature-specific pieces under `features/<name>/`.
- Tailwind utilities inline; share tokens via CSS variables in `index.css` (theme
  lives there, not in `tailwind.config`).
- Dark dashboard aesthetic: rounded tiles, one accent color, status dots, clean spacing.
- No component libraries (no shadcn/MUI). Hand-rolled UI only.

## 5. Ports

- `:443` HTTPS — SPA + API same-origin (Kestrel static files).
- `:8080` HTTP — API only (reserved for scripts).
- Never bind `:80` (Pi-hole) — never touch other running services.

## 6. Git & Workflow

- Commit only when the user asks. Small, single-purpose commits, imperative message.
- Don't commit generated output: `frontend/dist`, `wwwroot`, `bin/obj`, `artifacts/`,
  `certs/`, `*.pfx`.
- Secrets (DB password, certs) come from env vars (`ConnectionStrings__MyHomeLab`,
  `ASPNETCORE_Kestrel__Certificates__Default__Path`) — never commit them.

## 7. Definition of Done

- `dotnet build` clean, `npm run lint` + `npm run typecheck` clean.
- New behavior covered by a test (xUnit on backend; vitest for pure frontend helpers).
- Docs (`docs/implementation.md`) updated if the schema or API contract changed.
- If behavior on 443 changed, verify `https://<host>` still loads the SPA.