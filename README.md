# MyHomeLab

A self-hosted hub for every application running on this server. One clean,
modern dashboard where each service is a card — live health status, one-click open —
and the registry itself is fully editable (CRUD) and stored in Postgres.

## Stack

- **Backend**: .NET 10 / ASP.NET Core, DDD layering, Dapper + Npgsql, raw SQL migrations
- **Frontend**: React 18 + TypeScript + Vite + Tailwind CSS v4
- **Serving**: one process (Kestrel) — SPA + API on `:443` (HTTPS), API also on `:8080`
- **Data**: existing server Postgres, database `myhomelab`

## Quick start

See `docs/implementation.md` for the full design. In short:

```powershell
scripts/create-db.ps1   # once: create the Postgres database
scripts/dev.ps1         # API on :8080 + frontend dev server
scripts/build.ps1       # production build (frontend -> API wwwroot -> single deployable)
scripts/install-service.ps1  # run as a Windows service (optional, reboot-safe)
```

## Rules for AI / agents

If you're an AI coding agent, read `AGENTS.md` first — it contains the codebase
conventions and command set to follow.