# ShiftTrack Monorepo

ShiftTrack contains:
- `shifttrack-api`: .NET 8 minimal API (auth, users, calendar, realtime events).
- `shifttrack-web`: React + Vite app (login, employees, shift calendar).

This file is the project-level guide. Repo-specific details live in each subproject README.

## Current stack
- Backend: ASP.NET Core 8, Dapper, SQL Server, ACS Email, SignalR.
- Frontend: React 19, TypeScript, Vite, React Router, SignalR client.
- Infra: Azure App Service (API), Azure Static Web App (web), Azure SQL.

## Realtime status
Realtime schedule updates are implemented with SignalR:
- Hub endpoint: `/hubs/schedule`
- Event: `schedule.updated`
- Triggered from API when a user/schedule is created, updated, or deleted.
- Calendar page subscribes and refreshes data automatically.

## Local development

### 1) API
```bash
cd shifttrack-api
dotnet restore
dotnet run
```

### 2) Web
```bash
cd shifttrack-web
npm install
npm run dev
```

Web env (`shifttrack-web/.env.development` or `.env.local`):
```env
VITE_API_BASE=http://localhost:5000
```

If your API runs in another port, update `VITE_API_BASE`.

## Branching and PR flow
- Production branch: `prod`
- Integration branch: `dev`
- Work branches: `feature/<short-name>`, `fix/<short-name>`, `chore/<short-name>`

Recommended flow:
1. Update local `dev`.
2. Create a branch from `dev`.
3. Implement and validate locally.
4. Push branch and open PR to `dev`.
5. Merge to `dev`, validate in QA.
6. Promote to `prod` with controlled PR/release flow.

## Commit and PR guidelines
- Use small, focused commits.
- Suggested commit prefixes:
  - `feat:`
  - `fix:`
  - `chore:`
  - `refactor:`
  - `docs:`
  - `test:`
- Validate before PR:
  - API: `dotnet build`
  - Web: `npm run build`
- PR should include:
  - What changed
  - Why it changed
  - How it was tested
  - Screenshots for UI changes
  - Config/env changes if any

## Common issues
- API 500 at startup:
  - Check SQL connection string value and server reachability.
  - Ensure wrong `ConnectionStrings__Default` is not injected by pipeline/app settings.
- Front cannot call API:
  - Verify `VITE_API_BASE`.
  - Verify CORS `AllowedOrigins`.
- Swagger `GET /users` returns 403:
  - Add header `X-Role: 1` or `X-Role: 2`.

## Quick links
- API docs: `shifttrack-api/README.md`
- Web docs: `shifttrack-web/README.md`
