# Windows / IIS / SQL Server installer

`ATMTicketing.Deploy.exe` is a self-contained Windows console tool that does the
three things a fresh deployment needs, in order:

1. **Publish** — runs `dotnet publish` on `src/ATMTicketing.Web` (Release,
   framework-dependent) into your chosen folder, and writes an
   `appsettings.Production.json` there with your real SQL Server connection
   string and a freshly generated random JWT signing key (never the
   development placeholder from the checked-in `appsettings.json`).
2. **Database** — connects to your SQL Server instance, creates the database
   if it doesn't already exist, and applies `database/01_Schema.sql` through
   `05_SeedData.sql` in order (the exact same scripts documented in
   `docs/02-Deployment-Guide.md`). Safe to re-run: existing objects are
   detected and skipped rather than erroring out.
3. **IIS** — creates (or updates) an application pool set to "No Managed
   Code" and an IIS site bound to your chosen port, points it at the
   published folder, grants the application pool identity the NTFS
   permissions it needs (read/execute on the app, modify on
   `wwwroot/uploads` for ticket attachments), and starts both.

It is self-contained (bundles its own .NET runtime), so running it needs
nothing pre-installed on the target machine — only `dotnet publish` (step 1)
needs the .NET 8 SDK there, and only if you don't pass `--skip-publish`.

## Prerequisites on the target Windows machine

- **IIS** with the "Web Server (IIS)" role and its Management Tools
  installed (so `appcmd.exe` exists).
- **.NET 8 Hosting Bundle** — <https://dotnet.microsoft.com/download/dotnet/8.0>,
  the "Hosting Bundle" under ASP.NET Core Runtime 8.0.x. This registers the
  ASP.NET Core Module (ANCM) that IIS needs to run the app; without it the
  site returns HTTP 500.19/502.5. The installer warns you if it looks
  missing but does not install it for you.
- **SQL Server** (any edition — Express is fine) reachable from this
  machine, with either Windows (integrated) or SQL authentication.
- **.NET 8 SDK** — only needed for the publish step
  (<https://dotnet.microsoft.com/download/dotnet/8.0>, the SDK, not just
  the runtime). Skip this if you'll publish separately and pass
  `--skip-publish`.
- Run the installer **as Administrator** — it configures IIS and creates a
  database, both of which need elevation.

## Building the installer

The `.exe` itself is not committed to the repo (it's a large, rebuildable
binary — standard practice is to build it from source). From a machine with
the .NET 8 SDK (this can be any OS — the output always targets Windows):

```bash
cd deploy/ATMTicketing.Deploy
dotnet publish -c Release
```

The finished `ATMTicketing.Deploy.exe` is written to
`deploy/ATMTicketing.Deploy/bin/Release/net8.0/win-x64/publish/`. Copy that
one file to the Windows/IIS/SQL Server machine (or build directly on it) —
it's self-contained, no other files from that `publish` folder are needed.

## Running it

From an elevated Command Prompt or PowerShell, on the target machine, with
the full repo checked out next to (or above) the exe:

```powershell
.\ATMTicketing.Deploy.exe --sql-server ".\SQLEXPRESS" --database AtmTicketingDb --port 8080
```

Run it with no arguments for a fully interactive prompt-driven walkthrough,
or `--help` to see every switch (SQL authentication, custom site/app-pool
names, physical path, skipping individual steps, `--yes` to skip the
confirmation prompt for scripted use). Every step is safe to re-run — the
installer reuses/updates existing database objects and IIS objects instead
of failing on them, so re-running after fixing a problem (e.g. installing
the missing Hosting Bundle) picks up where it left off.

On success it prints the URL to browse to and a reminder to change the
seeded admin password (`admin@atmticketing.local` / `Admin@12345`).

## What it does NOT do

- Install SQL Server, IIS, or the .NET Hosting Bundle themselves — it
  checks for and warns about the Hosting Bundle, but installing system
  roles/features is left to you (or your organization's standard image),
  since that's a machine-wide, often policy-controlled change.
- Configure HTTPS/TLS bindings or a certificate — it binds plain HTTP on
  the port you choose. Put IIS behind your organization's TLS termination
  (a reverse proxy, an IIS HTTPS binding with a real certificate, etc.)
  before exposing it beyond localhost; see `docs/03-Security-Best-Practices.md`.
- Open firewall ports — if you need the site reachable from other
  machines, allow the chosen port through Windows Firewall separately.
