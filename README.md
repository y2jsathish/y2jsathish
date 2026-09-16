# ATM Call Log & Ticket Management System

Enterprise ATM operations and support-management platform: incident
logging, engineer dispatch, SLA tracking, escalations, and reporting
across a multi-region ATM estate (built for 10,000+ ATM locations).

## Tech stack

- **Frontend**: ASP.NET Core MVC + Razor Views, Bootstrap 5, DataTables
  (server-side processing), Chart.js, vanilla JS + jQuery, dark-mode theme
- **Backend**: ASP.NET Core 8, EF Core, Repository + Unit of Work,
  service-layer architecture, dependency injection, REST APIs
- **Database**: Microsoft SQL Server
- **Auth**: ASP.NET Core Identity, role-based access control (5 roles),
  JWT for external API clients, audit logging

## Solution layout

```
ATMTicketing.sln
├── src/ATMTicketing.Domain          # Entities & enums
├── src/ATMTicketing.Application     # DTOs, service/repository interfaces
├── src/ATMTicketing.Infrastructure  # EF Core, repositories, services, SLA monitor
├── src/ATMTicketing.Web             # MVC controllers, Razor views, REST API
├── tests/ATMTicketing.Tests         # xUnit: service-layer unit tests + real HTTP end-to-end tests
├── database/                        # Hand-written SQL Server schema, indexes, SPs, views, seed data
└── docs/                            # Architecture, deployment, security, hosting, API reference
```

See [`docs/01-Architecture.md`](docs/01-Architecture.md) for the full
design, [`docs/02-Deployment-Guide.md`](docs/02-Deployment-Guide.md) to
run it locally, and [`docs/05-API-Documentation.md`](docs/05-API-Documentation.md)
for the REST surface.

## Quick start

```bash
dotnet restore ATMTicketing.sln
dotnet ef database update --project src/ATMTicketing.Infrastructure --startup-project src/ATMTicketing.Web
dotnet run --project src/ATMTicketing.Web
```

Default seeded login: `admin@atmticketing.local` / `Admin@12345`
(**rotate before any shared/production use**).

## Testing

```bash
dotnet test tests/ATMTicketing.Tests/ATMTicketing.Tests.csproj
```

Two layers, both runnable without a real SQL Server instance (EF Core's
in-memory provider stands in for it):

- **Unit tests** (`tests/ATMTicketing.Tests/Services/`) — exercise the
  service layer (`SlaService`, `AssignmentService`, `TicketService`,
  `DashboardService`) against a real `ApplicationDbContext` +
  `UnitOfWork` + Identity `UserManager`/`RoleManager`, just backed by an
  isolated in-memory database per test instead of SQL Server.
- **End-to-end tests** (`tests/ATMTicketing.Tests/EndToEnd/`) — boot the
  actual `ATMTicketing.Web` app in-process via `WebApplicationFactory`
  and drive it with real HTTP requests: login, RBAC-gated pages,
  antiforgery-protected form posts, the DataTables JSON endpoints the UI
  calls, and the JWT-secured REST API — the same code paths a browser or
  external client would hit, without needing a live database or browser.

36 tests total (27 unit + 9 end-to-end), all currently passing.

## Roles

| Role | Capabilities |
|---|---|
| Administrator | Users, roles, SLA config, ATM/vendor masters, dashboards, settings |
| Call Center Agent | Create/search ATM tickets, view status, escalate |
| Field Engineer | View assigned tickets, update progress, notes, photos, close resolved tickets |
| Team Lead | Assign/reassign, monitor engineer workload & SLA, escalation management |
| Operations Manager | Dashboards, reports, ticket analytics, SLA compliance |

## Modules

Login & user management · ATM master (with Excel import) · Vendor master
· Ticket creation & workflow (New → Assigned → In Progress → Resolved →
Closed, with Pending/Escalated/Cancelled branches) · Automatic
region/workload-based assignment engine · SLA management (per-priority
response/resolution targets, live countdown, breach notifications) ·
Attachments & work notes · Email/in-app notifications · KPI dashboard
with Chart.js widgets · 7 exportable reports (Excel & PDF) · Full audit
trail.

## Database

`database/01_Schema.sql` → `05_SeedData.sql` is the standalone,
hand-authored SQL Server schema (tables, PKs/FKs, indexes, constraints,
stored procedures, views) — apply it directly for DBA-managed
environments, or use EF Core migrations for application-managed
environments (both are documented in the deployment guide).
