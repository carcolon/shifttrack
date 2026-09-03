# ShiftTrack API

ASP.NET Core 8 minimal API for ShiftTrack. This service handles authentication, employee management, schedule storage, PTO and change-request workflows, Colombian holiday data, exports, email notifications and realtime schedule events.

## Solution Layout

- `ShiftTrack.Api` - HTTP host, endpoints, migrations, workflow composition
- `ShiftTrack.Application` - interfaces and role helpers
- `ShiftTrack.Domain` - core entities
- `ShiftTrack.Infrastructure` - EF Core persistence and infrastructure services
- `tests` - unit, integration, acceptance, shared assets

## Tech Stack

- .NET 8
- ASP.NET Core Minimal API
- EF Core + SQL Server
- SignalR
- Azure Communication Services email
- Azure Key Vault configuration support
- Microsoft Entra ID integration

## Main Capabilities

### Authentication

- email/password login
- forgot/reset password flow
- forced password change
- Entra ID callback/login
- cookie auth + CSRF protection
- `/auth/me`
- `/auth/ping`
- `/auth/logout`
- 60-minute session timeout with rotation threshold

### Roles

- `0` Employee
- `1` Manager
- `2` Admin
- `3` Team Leader

### Role behavior

- Employee:
  - view schedule
  - request leave
- Team Leader:
  - same base permissions as Employee
  - can view coverage
- Manager:
  - request PTO for others
  - approve requests
  - manage users except Admin escalation restrictions
- Admin:
  - full user-management and review permissions

## Business Rules Implemented

### PTO

- Employees and Team Leaders cannot request PTO in the past
- Employees and Team Leaders cannot request PTO more than 60 days into the future
- Managers and Admins can submit PTO for past dates when permitted
- Vacation requests count only Colombian business days:
  - Monday to Friday
  - excluding Colombian holidays
- Saturdays, Sundays and holidays inside a vacation span are still marked in the schedule but do not consume vacation days

### Change Request / Swap

- same-role change requests
- requester must be working on the coworker day-off date
- offered requester day off must align to a day the coworker is working
- approval applies the schedule swap on both dates

### Calendar

- weekly schedule endpoint
- role filtering
- operation/company/shift filtering
- coverage calculation
- export support
- Colombian holidays endpoint for frontend highlighting

## HTTP Endpoints

### Auth

- `POST /auth/login`
- `POST /auth/entra-login`
- `POST /auth/entra-code-login`
- `GET /auth/me`
- `POST /auth/ping`
- `POST /auth/logout`
- `POST /auth/forgot-password`
- `POST /auth/reset-password`
- `POST /auth/reset-password/exchange-code`
- `POST /auth/reset-password/complete`
- `POST /auth/force-change-password`

### Users

- `POST /users`
- `GET /users`
- `GET /users/{id}`
- `PUT /users/{id}`
- `DELETE /users/{id}`

### Schedule and requests

- `GET /calendar`
- `POST /schedule/pto`
- `POST /schedule/pto/{requestId}/review`
- `POST /schedule/swap`
- `POST /schedule/swap/{requestId}/review`
- `GET /schedule/requests`
- `GET /schedule/events`
- `GET /schedule/export`

### Holidays

- `GET /holidays`

Supported query parameters:

- `year`
- `startDate`
- `endDate`
- `countryCode` default `CO`

### Assistant

- `POST /assistant/query`

## Realtime

- Hub path: `/hubs/schedule`
- Event name: `schedule.updated`

Published when user or schedule state changes.

## Configuration

Use `appsettings.Development.json`, environment variables, Azure App Service settings, or Azure Key Vault.

Example minimum shape:

```json
{
  "FrontendBaseUrl": "http://localhost:5173",
  "AllowedOrigins": [ "http://localhost:5173" ],
  "ConnectionStrings": {
    "Default": "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=ShiftTrack;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  },
  "Jwt": {
    "SigningKey": "<signing-key>",
    "Issuer": "ShiftTrack",
    "Audience": "ShiftTrack.Client"
  },
  "Auth": {
    "SessionTimeoutMinutes": 60,
    "SessionRotationThresholdMinutes": 10
  },
  "AzureAd": {
    "TenantId": "",
    "ClientId": "",
    "ClientSecret": ""
  },
  "EmailSettings": {
    "Mode": "AccessKey",
    "Endpoint": "https://<resource>.communication.azure.com/",
    "AccessKey": "<acs-key>",
    "Email": "donotreply@<domain>",
    "DisplayName": "ShiftTrack",
    "LogoPath": ""
  }
}
```

### Key Vault

If `KeyVault:VaultUri` is present, the API loads configuration from Azure Key Vault using `DefaultAzureCredential`. You can optionally set:

- `KeyVault:ManagedIdentityClientId`

## Database Migrations

Historical SQL migrations live in:

- `ShiftTrack.Api/Database/Migrations`

These scripts are kept as historical reference for the schema that existed before EF Core. They are not packaged for deployment. Dev/prod migration execution now uses EF Core only.

EF Core is configured in `ShiftTrack.Infrastructure.Persistence`. New schema changes should use EF migrations:

```bash
dotnet ef migrations add <MigrationName> --project ShiftTrack.Infrastructure --startup-project ShiftTrack.Api --context ShiftTrackDbContext --output-dir Persistence\Migrations
dotnet ef database update --project ShiftTrack.Infrastructure --startup-project ShiftTrack.Api --context ShiftTrackDbContext
```

`InitialEfBaseline` is intentionally empty because the existing schema was already created by the historical SQL scripts. Applying it only creates EF migration history metadata and records the EF model snapshot, so future migrations are generated from the current schema. The app and `--migrate-database` flow apply pending EF migrations only.

Before applying EF migrations, the app validates that the historical baseline tables already exist. This prevents accidentally marking an incomplete database as baseline-compatible.

## Colombian Holidays

The API persists holiday data in `dbo.Holidays` and exposes it through `/holidays`. Seeded data currently covers:

- `2025`
- `2026`
- `2027`
- `2028`

This supports:

- calendar holiday highlighting
- vacation business-day counting
- future admin/manual holiday management if needed

## Local Development

From the backend root:

```powershell
dotnet restore
dotnet run --project ShiftTrack.Api
```

Swagger local URL is available in development through the configured local port, commonly:

- `http://localhost:5243/swagger`

## Build

```powershell
cd shifttrack-api
dotnet build ShiftTrack.Api\ShiftTrack.Api.csproj
```

## Tests

The test structure is:

- `tests/Unit`
- `tests/Integration`
- `tests/Acceptance`
- `tests/Shared`

Shared assets include:

- builders
- mocks
- fixtures
- support utilities

### Fast test runner

```powershell
.\test.ps1 unit
.\test.ps1 integration
.\test.ps1 acceptance
.\test.ps1 all
```

### Direct commands

```powershell
dotnet test tests\Unit\ShiftTrack.Api.Tests\ShiftTrack.Api.Tests.csproj -p:UseAppHost=false
dotnet test tests\Integration\ShiftTrack.Api.IntegrationTests\ShiftTrack.Api.IntegrationTests.csproj -p:UseAppHost=false
dotnet test tests\Acceptance\ShiftTrack.Api.AcceptanceTests\ShiftTrack.Api.AcceptanceTests.csproj -p:UseAppHost=false
```

## Deployment Notes

- keep `FrontendBaseUrl` aligned with the deployed frontend
- keep `AllowedOrigins` aligned with the deployed frontend
- do not point production at a localhost frontend URL
- ensure app settings contain a valid SQL connection string
- ensure holiday migrations are deployed before relying on `/holidays`
- use separate infrastructure for production:
  - Static Web App
  - App Service
  - SQL Database
  - monitoring and secrets

## Troubleshooting

- `Cannot open server ... requested by the login`
  - your IP is not allowed by Azure SQL firewall
- `Format of the initialization string...`
  - malformed connection string
- `No such host is known`
  - SQL host or DNS issue
- `duplicate key ... UX_Holidays_Date_CountryCode`
  - duplicate holiday date inserted for the same country
- frontend links in emails point to localhost
  - check `FrontendBaseUrl` and `AllowedOrigins`
- `/holidays` returns `404` in cloud
  - backend deployment is behind local changes or migrations not deployed

## Recommended PR Checklist

- `dotnet build ShiftTrack.Api\ShiftTrack.Api.csproj` passes
- relevant tests pass
- migrations reviewed for idempotence and duplicate-key risk
- config changes documented
- no secrets committed
