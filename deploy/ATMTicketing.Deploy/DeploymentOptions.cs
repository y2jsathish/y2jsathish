namespace ATMTicketing.Deploy;

/// <summary>
/// Everything the installer needs, gathered from command-line switches first, falling back
/// to an interactive prompt for anything left unset — so it works both as a double-click
/// wizard and as a scriptable `Deploy.exe --yes --sql-server ... --database ...` call.
/// </summary>
public class DeploymentOptions
{
    public string SqlServer { get; set; } = string.Empty;
    public string Database { get; set; } = "AtmTicketingDb";
    public bool UseSqlAuth { get; set; }
    public string SqlUser { get; set; } = string.Empty;
    public string SqlPassword { get; set; } = string.Empty;

    public string SiteName { get; set; } = "ATMTicketing";
    public string AppPoolName { get; set; } = "ATMTicketingPool";
    public int Port { get; set; } = 8080;
    public string PhysicalPath { get; set; } = @"C:\inetpub\wwwroot\ATMTicketing";

    /// <summary>Repo root (the folder containing src/ATMTicketing.Web) to publish from.</summary>
    public string SourceRoot { get; set; } = string.Empty;

    public bool SkipSql { get; set; }
    public bool SkipIis { get; set; }
    public bool SkipPublish { get; set; }
    public bool AssumeYes { get; set; }

    public static DeploymentOptions Parse(string[] args)
    {
        var options = new DeploymentOptions();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--sql-server": options.SqlServer = Next(args, ref i); break;
                case "--database": options.Database = Next(args, ref i); break;
                case "--sql-auth": options.UseSqlAuth = true; break;
                case "--sql-user": options.SqlUser = Next(args, ref i); break;
                case "--sql-password": options.SqlPassword = Next(args, ref i); break;
                case "--site-name": options.SiteName = Next(args, ref i); break;
                case "--app-pool": options.AppPoolName = Next(args, ref i); break;
                case "--port": options.Port = int.Parse(Next(args, ref i)); break;
                case "--physical-path": options.PhysicalPath = Next(args, ref i); break;
                case "--source": options.SourceRoot = Next(args, ref i); break;
                case "--skip-sql": options.SkipSql = true; break;
                case "--skip-iis": options.SkipIis = true; break;
                case "--skip-publish": options.SkipPublish = true; break;
                case "--yes":
                case "-y": options.AssumeYes = true; break;
                case "--help":
                case "-h": PrintHelpAndExit(); break;
                default:
                    Console.WriteLine($"Unrecognized argument '{args[i]}' — ignoring. Run with --help to see all options.");
                    break;
            }
        }

        return options;
    }

    private static string Next(string[] args, ref int i)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value after '{args[i]}'.");
        }
        return args[++i];
    }

    private static void PrintHelpAndExit()
    {
        Console.WriteLine("""
            ATM Ticketing System — installer

            Publishes the ASP.NET Core app, provisions the SQL Server database from
            database/*.sql, and configures an IIS site + application pool.
            Run this from an elevated (Administrator) command prompt.

            Options (all optional — anything not given is prompted for interactively):
              --sql-server <name>      SQL Server instance, e.g. ".\SQLEXPRESS" or "localhost"
              --database <name>        Database name (default: AtmTicketingDb)
              --sql-auth                Use SQL Server authentication instead of Windows auth
              --sql-user <user>         SQL Server login (with --sql-auth)
              --sql-password <pass>     SQL Server password (with --sql-auth)
              --site-name <name>       IIS site name (default: ATMTicketing)
              --app-pool <name>        IIS application pool name (default: ATMTicketingPool)
              --port <n>               HTTP port to bind (default: 8080)
              --physical-path <path>   Deployment folder (default: C:\inetpub\wwwroot\ATMTicketing)
              --source <path>          Repo root containing src\ATMTicketing.Web (default: auto-detected)
              --skip-sql               Skip database provisioning
              --skip-iis               Skip IIS site/app pool configuration
              --skip-publish            Skip `dotnet publish` (physical-path must already contain a build)
              --yes, -y                 Don't ask for confirmation before making changes
              --help, -h                 Show this help
            """);
        Environment.Exit(0);
    }
}
