# Architecture

## Overview

The ATM Call Log & Ticket Management System is built as a 4-project
Clean Architecture solution on ASP.NET Core 8 MVC, with a SQL Server
backing store accessed through EF Core.

```
ATMTicketing.sln
├── src/ATMTicketing.Domain          # Entities, enums — zero dependencies
├── src/ATMTicketing.Application     # DTOs, repository/service interfaces — depends on Domain only
├── src/ATMTicketing.Infrastructure  # EF Core, repositories, services, SLA background job — depends on Application
└── src/ATMTicketing.Web             # MVC controllers, Razor views, wwwroot — depends on all of the above
```

Dependencies point inward only: Web → Infrastructure → Application → Domain.
Nothing in Domain or Application references EF Core, ASP.NET Core, or any
other framework package — they are plain C# class libraries, which keeps
the business rules (SLA calculation, assignment logic, workflow
transitions) testable without a database or web host.

## Layers

**Domain** (`ATMTicketing.Domain`)
Entities (`Ticket`, `Atm`, `Vendor`, `TicketAssignment`, …) and enums
(`PriorityLevel`, `TicketStatusCode`, …). `ApplicationUser`/`ApplicationRole`
extend ASP.NET Core Identity's `IdentityUser`/`IdentityRole` so RBAC is
handled by Identity rather than a hand-rolled permissions table.

**Application** (`ATMTicketing.Application`)
- `Interfaces/Repositories` — `IGenericRepository<T>`, `IUnitOfWork`
- `Interfaces/Services` — one interface per business capability
  (`ITicketService`, `ISlaService`, `IAssignmentService`, …)
- `DTOs` — the only types that cross into Razor views/controllers;
  entities never leak past the service layer
- `Common` — `ServiceResult`/`ServiceResult<T>` (uniform success/failure
  envelope) and `PagedResult<T>`/`DataTableResponse<T>` (DataTables
  server-side paging contract)

**Infrastructure** (`ATMTicketing.Infrastructure`)
- `Persistence/ApplicationDbContext` — `IdentityDbContext<ApplicationUser, ApplicationRole, string>`
- `Persistence/Configurations` — Fluent API entity configurations (one
  class per entity, `IEntityTypeConfiguration<T>`)
- `Persistence/Repositories` — `GenericRepository<T>` + `UnitOfWork`
- `Persistence/Seed/DbInitializer` — idempotent startup seed (roles,
  status/category masters, SLA policy, demo regions/vendors/ATMs, admin user)
- `Services/*` — the actual business logic: `TicketService`,
  `AtmService`, `VendorService`, `SlaService`, `AssignmentService`,
  `NotificationService`, `DashboardService`, `ReportService`, `AuditService`
- `BackgroundServices/SlaMonitorService` — a `BackgroundService` that
  polls open tickets every minute, flags SLA breaches/warnings, and
  raises notifications

**Web** (`ATMTicketing.Web`)
- MVC controllers per module (`AtmController`, `TicketController`, …)
  plus a `Controllers/Api` folder for the JWT-secured REST surface
  (`/api/tickets`, `/api/atms`, `/api/auth/token`) used by external/mobile
  clients, separate from the cookie-authenticated browser routes
- Razor views: Bootstrap 5 + DataTables (server-side processing) +
  Chart.js, corporate-blue theme with a dark-mode toggle
  (`wwwroot/css/site.css`, `wwwroot/js/site.js`)

## Cross-cutting concerns

- **Repository Pattern + Unit of Work** — `IUnitOfWork` exposes one
  `IGenericRepository<T>` per aggregate; `SaveChangesAsync()` commits
  everything in a single transaction per web request (EF Core's own
  `DbContext` change tracking backs the "unit of work").
- **Dependency Injection** — `Infrastructure/DependencyInjection.cs`
  (`AddInfrastructure()`) registers EF Core, Identity, the SLA
  background service, and every application service as scoped;
  `Program.cs` wires cookie + JWT authentication schemes and the
  role-based authorization policies on top.
- **SLA engine** — `SlaService.CalculateDueDates` computes
  `ResponseDueAt`/`ResolutionDueAt` from `SlaConfiguration` at ticket
  creation; `SlaMonitorService` re-evaluates all open tickets each
  minute so a breach is caught even if nobody is viewing the ticket.
- **Auto-assignment engine** — `AssignmentService.FindBestEngineerAsync`
  picks the field engineer in the ticket's region with the lowest
  current open-ticket count (falls back to any active engineer, then to
  no assignment for manual Team Lead triage).
- **Audit trail** — `AuditActionFilter` (an `IAsyncActionFilter`) logs
  every successful state-changing MVC action; `TicketHistory` separately
  captures the ticket-specific timeline (status changes, assignments,
  escalations, notes) shown on the ticket details page.
- **REST + JWT** — browser traffic authenticates via the ASP.NET Core
  Identity cookie (`IdentityConstants.ApplicationScheme`); the
  `Controllers/Api` controllers additionally accept a JWT bearer token
  issued by `POST /api/auth/token`, so external systems/mobile apps can
  integrate without a browser session.
