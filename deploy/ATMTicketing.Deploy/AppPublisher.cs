using System.Diagnostics;

namespace ATMTicketing.Deploy;

public static class AppPublisher
{
    /// <summary>Walks upward from the installer's own folder looking for a directory that
    /// contains src\ATMTicketing.Web\ATMTicketing.Web.csproj, so the exe can be run either
    /// from a `deploy\` subfolder of the checked-out repo or from anywhere else once given
    /// an explicit --source.</summary>
    public static string? TryLocateSourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "ATMTicketing.Web", "ATMTicketing.Web.csproj");
            if (File.Exists(candidate))
            {
                return dir.FullName;
            }
        }
        return null;
    }

    public static void PublishAndDeploy(DeploymentOptions options)
    {
        if (options.SkipPublish)
        {
            Console.WriteLine("Skipping publish (--skip-publish) — using whatever is already in the physical path.");
            if (!Directory.Exists(options.PhysicalPath) || !Directory.GetFiles(options.PhysicalPath).Any())
            {
                throw new InvalidOperationException(
                    $"--skip-publish was given but '{options.PhysicalPath}' is empty or missing. Publish the app there first.");
            }
            return;
        }

        var csproj = Path.Combine(options.SourceRoot, "src", "ATMTicketing.Web", "ATMTicketing.Web.csproj");
        if (!File.Exists(csproj))
        {
            throw new InvalidOperationException(
                $"Could not find {csproj}. Pass --source <repo root> pointing at the checked-out ATMTicketing repository.");
        }

        if (!IsDotnetAvailable())
        {
            throw new InvalidOperationException(
                "The .NET SDK ('dotnet' on PATH) is required to publish the app. Install the .NET 8 SDK " +
                "(https://dotnet.microsoft.com/download/dotnet/8.0) or publish manually and re-run with --skip-publish.");
        }

        Directory.CreateDirectory(options.PhysicalPath);

        Console.WriteLine($"Publishing ATMTicketing.Web (Release) to '{options.PhysicalPath}' ...");
        var publishArgs = $"publish \"{csproj}\" -c Release -o \"{options.PhysicalPath}\" --self-contained false";
        RunProcess("dotnet", publishArgs, options.SourceRoot);

        WriteProductionAppSettings(options);

        var uploadsDir = Path.Combine(options.PhysicalPath, "wwwroot", "uploads", "tickets");
        Directory.CreateDirectory(uploadsDir);

        Console.WriteLine("Publish complete.");
    }

    /// <summary>Writes appsettings.Production.json into the published output with the
    /// caller's real SQL Server connection string and a freshly generated JWT signing key —
    /// the checked-in appsettings.json only ever holds development placeholders and must
    /// never be the source of the production secret.</summary>
    private static void WriteProductionAppSettings(DeploymentOptions options)
    {
        var connectionString = options.UseSqlAuth
            ? $"Server={options.SqlServer};Database={options.Database};User Id={options.SqlUser};Password={options.SqlPassword};TrustServerCertificate=True;MultipleActiveResultSets=true"
            : $"Server={options.SqlServer};Database={options.Database};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        var jwtKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));

        // Escape for JSON string literals (backslashes/quotes only — these values don't
        // contain control characters).
        static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        var json = $$"""
            {
              "ConnectionStrings": {
                "DefaultConnection": "{{Esc(connectionString)}}"
              },
              "Jwt": {
                "Key": "{{Esc(jwtKey)}}"
              }
            }
            """;

        var path = Path.Combine(options.PhysicalPath, "appsettings.Production.json");
        File.WriteAllText(path, json);
        Console.WriteLine($"Wrote {path} with the real connection string and a freshly generated JWT signing key.");
    }

    private static bool IsDotnetAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using var process = Process.Start(psi);
            process?.WaitForExit(10_000);
            return process is { ExitCode: 0 };
        }
        catch
        {
            return false;
        }
    }

    private static void RunProcess(string fileName, string arguments, string? workingDirectory = null)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start '{fileName}'.");
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) Console.WriteLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Console.Error.WriteLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"'{fileName} {arguments}' exited with code {process.ExitCode}.");
        }
    }
}
