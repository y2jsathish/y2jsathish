# Production Hosting Guide

This system is a standard ASP.NET Core 8 MVC app + SQL Server, so any of
the following hosting models work. Pick based on your existing
infrastructure — nothing in the codebase is cloud-specific.

## Option A — IIS on Windows Server (traditional bank/enterprise datacenter)

1. Install the **.NET 8 Hosting Bundle** on the IIS server.
2. `dotnet publish src/ATMTicketing.Web -c Release -o ./publish`
3. Create an IIS site pointing at `./publish`, App Pool set to
   **No Managed Code** (ASP.NET Core Module handles the CLR).
4. Bind HTTPS with a certificate from your internal/enterprise CA.
5. Set connection strings and `Jwt:Key`/`Smtp:*` via `web.config`
   environment variables or, preferably, machine-level environment
   variables so secrets never live in the deployed `appsettings.json`.
6. Point `ConnectionStrings:DefaultConnection` at a SQL Server instance
   reachable from the IIS box (same VLAN, firewall rule for TCP 1433).

## Option B — Linux + systemd + Nginx reverse proxy

1. `dotnet publish src/ATMTicketing.Web -c Release -o /var/www/atm-ticketing`
2. Create a systemd unit (`/etc/systemd/system/atm-ticketing.service`)
   running `dotnet /var/www/atm-ticketing/ATMTicketing.Web.dll`,
   `Restart=always`, `Environment=ASPNETCORE_ENVIRONMENT=Production`.
3. Nginx terminates TLS and reverse-proxies to Kestrel on
   `http://127.0.0.1:5000`; forward `X-Forwarded-For`/`X-Forwarded-Proto`
   and enable `UseForwardedHeaders()` in `Program.cs` if not already
   present for your proxy setup.
4. SQL Server for Linux, or a managed SQL Server instance reachable over
   the network.

## Option C — Containers (Docker / Kubernetes)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/ATMTicketing.Web -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "ATMTicketing.Web.dll"]
```

- Inject configuration via environment variables / Kubernetes
  Secrets+ConfigMaps (`ConnectionStrings__DefaultConnection`, `Jwt__Key`, …).
- Mount a persistent volume for `wwwroot/uploads` (ticket attachments),
  or switch that storage call to Azure Blob Storage / S3 so pods stay
  stateless and scale horizontally.
- Run `SlaMonitorService` in a single replica (or externalize the sweep
  to `usp_Sla_EvaluateBreaches` via a Kubernetes CronJob) to avoid
  duplicate breach notifications from multiple pods.
- SQL Server: Azure SQL Database, Amazon RDS for SQL Server, or a
  managed SQL Server instance — not a container for production data.

## Option D — Managed PaaS (Azure App Service)

1. `az webapp up` or a GitHub Actions / Azure DevOps pipeline running
   `dotnet publish` + `az webapp deploy`.
2. Azure SQL Database for the connection string; use a Managed Identity
   + `Authentication=Active Directory Managed Identity` connection
   string instead of a SQL login where possible.
3. Azure Key Vault references for `Jwt:Key` and `Smtp:Password` in App
   Service Configuration (`@Microsoft.KeyVault(...)` syntax) instead of
   plain app settings.
4. Azure Files mount (App Service "Storage Mounts") for
   `wwwroot/uploads`, or move attachments to Azure Blob Storage.
5. Enable Application Insights for the same telemetry the
   `ILogger<T>` calls throughout `Infrastructure/Services` already emit.

## Common to every option

- **Health checks**: add `builder.Services.AddHealthChecks().AddSqlServer(...)`
  and `app.MapHealthChecks("/health")` if your orchestrator/load
  balancer needs a liveness endpoint (not wired up by default — add it
  alongside your chosen hosting model).
- **Logging**: `ILogger<T>` is used throughout; wire it to your
  platform's sink (Application Insights, CloudWatch, ELK, Seq) via the
  standard `Microsoft.Extensions.Logging` provider packages in `Program.cs`.
- **Database migrations on deploy**: run `dotnet ef database update`
  (or the `database/*.sql` scripts) as a pre-traffic release step, never
  automatically inside `Program.cs` for a horizontally-scaled deployment
  (multiple instances racing to migrate on startup is a common outage
  cause) — this template's `DbInitializer.SeedAsync` calls
  `context.Database.MigrateAsync()` for local/dev convenience only;
  disable that call and run migrations out-of-band for production.
- **Backups & DR**: standard SQL Server backup/restore (full + log
  shipping or Always On Availability Groups for HA), matched to your
  RPO/RTO requirements for a 10,000+ ATM estate.
