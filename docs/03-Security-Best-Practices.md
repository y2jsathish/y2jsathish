# Security Best Practices

## Implemented in this codebase

- **Authentication** — ASP.NET Core Identity (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`)
  with hashed + salted passwords, account lockout after 5 failed
  attempts (15-minute lockout window), and a strong password policy
  (`Infrastructure/DependencyInjection.cs`: min length 8, requires
  upper-case, digit, non-alphanumeric).
- **Authorization / RBAC** — five roles (`Administrator`,
  `CallCenterAgent`, `FieldEngineer`, `TeamLead`, `OperationsManager`)
  enforced both via `[Authorize(Roles = ...)]` on controllers/actions and
  via named policies (`CanManageTickets`, `CanAssignTickets`,
  `CanWorkTickets`, `CanViewReports`) in `Program.cs`, so the mapping
  from role → capability lives in one place.
- **CSRF protection** — every state-changing form uses
  `[ValidateAntiForgeryToken]` server-side and the ASP.NET Core
  anti-forgery cookie/token pair (automatic with `asp-action`/`asp-controller`
  tag helpers) client-side.
- **Session management** — sliding 8-hour Identity cookie expiry,
  `HttpOnly` + `Secure` cookie flags, plus a 30-minute idle
  `AddSession` timeout for transient UI state.
- **Transport security** — `UseHttpsRedirection()` + `UseHsts()` in
  non-development environments.
- **SQL injection** — all data access goes through EF Core's
  parameterized LINQ queries; the one raw-SQL call
  (`SELECT NEXT VALUE FOR dbo.TicketNumberSequence`) takes no
  user-supplied input.
- **XSS** — Razor HTML-encodes all output by default; the few places
  that build HTML client-side from JSON (`site.js`, DataTables `render`
  callbacks) interpolate only server-controlled enum/status strings, not
  free-text user input.
- **File upload hardening** (`TicketController.UploadAttachment`) —
  extension allow-list (`.jpg .jpeg .png .pdf .docx .xlsx`), a 10 MB size
  cap enforced both client- and server-side (`[RequestSizeLimit]`), and
  server-generated random file names (`Guid.NewGuid()`) so uploaded
  filenames can never be used for path traversal or to overwrite another
  ticket's files.
- **Audit logging** — `AuditActionFilter` records every successful
  POST/PUT/DELETE MVC action (`AuditLog` table); `TicketHistory`
  additionally records the full ticket-specific change timeline; login
  success/failure is logged via `IAuditService.LogLoginAsync`.
- **JWT for the REST API** — short-lived (default 60 min, configurable),
  signed with `HmacSha256`, validated on issuer/audience/lifetime/key;
  kept on a separate authentication scheme from the browser cookie so a
  leaked API token can't be replayed as a full browser session cookie
  and vice versa.
- **Least-privilege data exposure** — controllers and API endpoints
  return DTOs (`Application/DTOs`), never EF entities, so navigation
  properties and internal fields (password hashes, security stamps)
  can't accidentally be serialized to a client.

## Operational checklist before going to production

1. **Rotate the seeded admin password** (`admin@atmticketing.local`)
   immediately after first deployment.
2. **Set `Jwt:Key` to a strong, random 256-bit+ secret** via environment
   variable or a secrets manager — the value in `appsettings.json` is a
   development-only placeholder and the app will throw if you forget to
   change it and an attacker discovers it in source control.
3. **Enforce HTTPS at the load balancer/reverse proxy** in addition to
   `UseHttpsRedirection()`, and terminate TLS with a certificate from a
   trusted CA (not the IIS Express/Kestrel dev cert).
4. **Configure `AddDataProtection().PersistKeysToFileSystem(...)` /
   `.PersistKeysToAzureBlobStorage(...)`** if running more than one
   instance, so Identity cookies and anti-forgery tokens remain valid
   across restarts and load-balanced instances.
5. **Restrict SQL Server network access** to the application's subnet;
   use a dedicated least-privilege SQL login for the app's connection
   string (db_datareader/db_datawriter + EXECUTE on the stored
   procedures it uses — not `db_owner`).
6. **Enable SMTP with authenticated, TLS-secured mail relay** and remove
   test/demo email addresses before go-live.
7. **Review and tighten CSP / security headers** — this codebase relies
   on ASP.NET Core defaults; add a `Content-Security-Policy`,
   `X-Content-Type-Options: nosniff`, and `Referrer-Policy` middleware
   (e.g. `NWebsec` or a custom middleware) appropriate to your CDN
   allow-list (the CDNs referenced in `_Layout.cshtml` — jsDelivr —
   should be pinned in the CSP `script-src`/`style-src`).
8. **Rate-limit `/api/auth/token`** (ASP.NET Core's built-in rate
   limiting middleware, `AddRateLimiter`) to slow down credential
   stuffing against the REST login endpoint; Identity's account lockout
   covers the MVC login form already.
9. **Regularly back up SQL Server** and test restore procedures;
   `AuditLog` and `TicketHistory` are your incident-investigation trail —
   back them up with the same retention as ticket data.
10. **Patch cadence** — track NuGet advisories for
    `Microsoft.AspNetCore.Identity.EntityFrameworkCore`,
    `Microsoft.EntityFrameworkCore.SqlServer`, `ClosedXML`, and
    `QuestPDF`; run `dotnet list package --vulnerable` in CI.
