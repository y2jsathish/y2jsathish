using System.Security.Principal;
using ATMTicketing.Deploy;

Console.WriteLine("=====================================================");
Console.WriteLine(" ATM Call Log & Ticket Management System — Installer");
Console.WriteLine("=====================================================");
Console.WriteLine();

if (!IsRunningElevated())
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("This installer must be run as Administrator (it configures IIS and SQL Server).");
    Console.WriteLine("Right-click Deploy.exe and choose 'Run as administrator', or launch it from an");
    Console.WriteLine("elevated Command Prompt / PowerShell window.");
    Console.ResetColor();
    return 1;
}

DeploymentOptions options;
try
{
    options = DeploymentOptions.Parse(args);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Argument error: {ex.Message}");
    return 1;
}

PromptForMissingValues(options);

Console.WriteLine();
Console.WriteLine("About to:");
if (!options.SkipPublish)
{
    Console.WriteLine($"  - Publish ATMTicketing.Web (Release) to: {options.PhysicalPath}");
}
if (!options.SkipSql)
{
    var auth = options.UseSqlAuth ? $"SQL login '{options.SqlUser}'" : "Windows (integrated) authentication";
    Console.WriteLine($"  - Provision database '{options.Database}' on '{options.SqlServer}' using {auth}");
}
if (!options.SkipIis)
{
    Console.WriteLine($"  - Create/update IIS site '{options.SiteName}' (pool '{options.AppPoolName}') on port {options.Port}");
}
Console.WriteLine();

if (!options.AssumeYes && !Confirm("Proceed? [y/N] "))
{
    Console.WriteLine("Cancelled — nothing was changed.");
    return 0;
}

try
{
    if (!options.SkipPublish)
    {
        Console.WriteLine();
        Console.WriteLine("--- Publishing application ---");
        AppPublisher.PublishAndDeploy(options);
    }

    if (!options.SkipSql)
    {
        Console.WriteLine();
        Console.WriteLine("--- Provisioning database ---");
        SqlDeployer.ProvisionDatabase(options);
    }

    if (!options.SkipIis)
    {
        Console.WriteLine();
        Console.WriteLine("--- Configuring IIS ---");
        IisDeployer.Configure(options);
    }
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Deployment failed: {ex.Message}");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine("Nothing after the failed step ran. Fix the issue above and re-run — every step");
    Console.WriteLine("is safe to repeat (existing database objects and IIS objects are reused/updated).");
    return 1;
}

Console.WriteLine();
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("Deployment finished successfully.");
Console.ResetColor();
Console.WriteLine();
if (!options.SkipIis)
{
    Console.WriteLine($"Browse to: http://localhost:{options.Port}/");
}
Console.WriteLine("Default seeded login: admin@atmticketing.local / Admin@12345");
Console.WriteLine("Change this password immediately (User Administration, once logged in).");
return 0;

static bool IsRunningElevated()
{
    if (!OperatingSystem.IsWindows())
    {
        return false;
    }
    using var identity = WindowsIdentity.GetCurrent();
    var principal = new WindowsPrincipal(identity);
    return principal.IsInRole(WindowsBuiltInRole.Administrator);
}

static bool Confirm(string prompt)
{
    Console.Write(prompt);
    var response = Console.ReadLine();
    return string.Equals(response?.Trim(), "y", StringComparison.OrdinalIgnoreCase);
}

static void PromptForMissingValues(DeploymentOptions options)
{
    if (string.IsNullOrWhiteSpace(options.SourceRoot))
    {
        options.SourceRoot = AppPublisher.TryLocateSourceRoot() ?? string.Empty;
    }
    if (!options.SkipPublish && string.IsNullOrWhiteSpace(options.SourceRoot))
    {
        options.SourceRoot = PromptString(
            "Path to the ATMTicketing repo root (containing 'src\\ATMTicketing.Web')",
            required: true);
    }

    if (!options.SkipSql && string.IsNullOrWhiteSpace(options.SqlServer))
    {
        options.SqlServer = PromptString(
            "SQL Server instance (e.g. .\\SQLEXPRESS, localhost, or a named server)",
            required: true);
    }

    if (!options.SkipSql && options.UseSqlAuth && string.IsNullOrWhiteSpace(options.SqlUser))
    {
        options.SqlUser = PromptString("SQL Server login name", required: true);
    }
    if (!options.SkipSql && options.UseSqlAuth && string.IsNullOrWhiteSpace(options.SqlPassword))
    {
        options.SqlPassword = PromptPassword("SQL Server password");
    }
}

static string PromptString(string label, bool required)
{
    while (true)
    {
        Console.Write($"{label}: ");
        var value = Console.ReadLine()?.Trim() ?? string.Empty;
        if (value.Length > 0 || !required)
        {
            return value;
        }
        Console.WriteLine("This value is required.");
    }
}

static string PromptPassword(string label)
{
    Console.Write($"{label}: ");
    var password = new System.Text.StringBuilder();
    ConsoleKeyInfo key;
    while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
    {
        if (key.Key == ConsoleKey.Backspace && password.Length > 0)
        {
            password.Length--;
        }
        else if (!char.IsControl(key.KeyChar))
        {
            password.Append(key.KeyChar);
        }
    }
    Console.WriteLine();
    return password.ToString();
}
