# Deployment Guide

## Prerequisites

- .NET 8 SDK
- SQL Server 2019+ (or LocalDB / SQL Server Express for development)
- Node is **not** required — Bootstrap, jQuery, DataTables and Chart.js
  are loaded from CDN (see `Views/Shared/_Layout.cshtml`)

## Local development

```bash
# 1. Restore & build
dotnet restore ATMTicketing.sln
dotnet build ATMTicketing.sln

# 2. Point the connection string at your SQL Server instance
#    (src/ATMTicketing.Web/appsettings.Development.json)

# 3. Create the database
#    Option A — EF Core migrations (recommended for local dev):
dotnet tool install --global dotnet-ef   # once
dotnet ef migrations add InitialCreate --project src/ATMTicketing.Infrastructure --startup-project src/ATMTicketing.Web
dotnet ef database update --project src/ATMTicketing.Infrastructure --startup-project src/ATMTicketing.Web

#    Option B — hand-written schema (DBA-managed environments):
#    run database/01_Schema.sql .. 05_SeedData.sql in order via sqlcmd/SSMS

# 4. Run
dotnet run --project src/ATMTicketing.Web
```

On first run, `DbInitializer` seeds roles, status/category masters, SLA
policy, demo regions/vendors/ATMs, and a default administrator:

```
admin@atmticketing.local / Admin@12345
```

**Change this password immediately in any shared/production environment.**

## Configuration

All configuration lives in `src/ATMTicketing.Web/appsettings.json`
(overridden per-environment by `appsettings.{Environment}.json` and, in
production, environment variables / a secrets manager — never commit
real secrets):

| Key | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string |
| `Jwt:Key` / `Jwt:Issuer` / `Jwt:Audience` / `Jwt:ExpiryMinutes` | REST API token signing |
| `Smtp:*` | Outbound email for notifications (leave `Smtp:Host` empty to disable email; in-app notifications still work) |

In production, set secrets via environment variables
(`ConnectionStrings__DefaultConnection`, `Jwt__Key`, `Smtp__Password`, …)
or `dotnet user-secrets` / Azure Key Vault / AWS Secrets Manager —
**never** check real credentials into `appsettings.json`.

## Database migrations in CI/CD

Run `dotnet ef database update` (or apply the numbered `database/*.sql`
scripts in order) as a release step before the new app version starts
serving traffic. Both paths are additive/idempotent by design — re-running
`05_SeedData.sql` or the migrations against an already-seeded database is
a no-op (`IF NOT EXISTS` guards / EF Core's `__EFMigrationsHistory`).

## File storage

Ticket attachments are written to `wwwroot/uploads/tickets/{ticketId}/`
on the web server's local disk by default (see
`TicketController.UploadAttachment`). For a horizontally-scaled or
containerized deployment, mount that path to shared/network storage (an
Azure Files share, an NFS mount, etc.) or swap the storage call for a
blob-storage SDK (Azure Blob Storage / S3) — the upload path is isolated
to one method, so this is a localized change.

## Scaling notes

- The app is stateless except for the Identity cookie and ASP.NET Core
  session (`AddSession`, used only for transient UI state) — both use
  the standard cookie mechanism, so multiple instances behind a load
  balancer work as long as sticky sessions or a distributed data
  protection key ring (`AddDataProtection().PersistKeysToXyz()`) is
  configured so cookies issued by one instance validate on another.
- `SlaMonitorService` runs as an in-process `BackgroundService`. If you
  scale to multiple instances, either run it in exactly one designated
  instance (feature flag / leader election) or move the sweep to
  `usp_Sla_EvaluateBreaches` on a SQL Agent job — both are provided.
- The system is designed for 10,000+ ATM locations: the heavy list
  endpoints (`Atm/GetData`, `Ticket/GetData`) use DataTables server-side
  processing (paged, filtered, sorted in SQL — never loading the full
  table into memory), and the indexes in `database/02_Indexes.sql` cover
  the filter/sort columns those endpoints use.
