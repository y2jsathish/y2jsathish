using System.Diagnostics;

namespace ATMTicketing.Deploy;

public static class IisDeployer
{
    private static string AppCmdPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), "inetsrv", "appcmd.exe");

    public static void Configure(DeploymentOptions options)
    {
        if (!File.Exists(AppCmdPath))
        {
            throw new InvalidOperationException(
                $"'{AppCmdPath}' was not found. Install the IIS 'Web Server (IIS)' role with " +
                "Management Tools, then re-run this installer.");
        }

        WarnIfHostingBundleMissing();

        Console.WriteLine($"Configuring IIS application pool '{options.AppPoolName}' ...");
        EnsureAppPool(options.AppPoolName);

        Console.WriteLine($"Configuring IIS site '{options.SiteName}' on port {options.Port} ...");
        EnsureSite(options);

        GrantFilePermissions(options);

        RunAppCmd($"start apppool \"{options.AppPoolName}\"", ignoreErrors: true);
        RunAppCmd($"start site \"{options.SiteName}\"", ignoreErrors: true);

        Console.WriteLine("IIS configuration complete.");
    }

    private static void EnsureAppPool(string appPoolName)
    {
        if (!ObjectExists($"list apppool \"{appPoolName}\""))
        {
            // managedRuntimeVersion: (empty) = "No Managed Code", required for ASP.NET Core's
            // out-of-process/in-process hosting via the ASP.NET Core Module.
            RunAppCmd($"add apppool /name:\"{appPoolName}\" /managedRuntimeVersion: /managedPipelineMode:Integrated");
        }

        RunAppCmd($"set apppool \"{appPoolName}\" /processModel.identityType:ApplicationPoolIdentity");
        RunAppCmd($"set apppool \"{appPoolName}\" /enable32BitAppOnWin64:false");
    }

    private static void EnsureSite(DeploymentOptions options)
    {
        var binding = $"http/*:{options.Port}:";

        if (!ObjectExists($"list site \"{options.SiteName}\""))
        {
            RunAppCmd($"add site /name:\"{options.SiteName}\" /physicalPath:\"{options.PhysicalPath}\" /bindings:{binding}");
        }
        else
        {
            Console.WriteLine($"Site '{options.SiteName}' already exists — updating its path and binding.");
            RunAppCmd($"set vdir \"{options.SiteName}/\" /physicalPath:\"{options.PhysicalPath}\"");
            RunAppCmd($"set site \"{options.SiteName}\" /bindings:{binding}", ignoreErrors: true);
        }

        RunAppCmd($"set app \"{options.SiteName}/\" /applicationPool:\"{options.AppPoolName}\"");
    }

    /// <summary>The ApplicationPoolIdentity needs read+execute on the deployed app and
    /// write access to the ticket-attachments upload folder — the most common cause of a
    /// working `dotnet publish` still 500'ing under IIS is missing NTFS permissions.</summary>
    private static void GrantFilePermissions(DeploymentOptions options)
    {
        var uploadsPath = Path.Combine(options.PhysicalPath, "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsPath);

        var identity = $"IIS AppPool\\{options.AppPoolName}";
        Console.WriteLine($"Granting '{identity}' read/execute on the site, and modify on wwwroot\\uploads ...");

        RunProcess("icacls", $"\"{options.PhysicalPath}\" /grant \"{identity}:(OI)(CI)RX\" /T /Q", ignoreErrors: true);
        RunProcess("icacls", $"\"{uploadsPath}\" /grant \"{identity}:(OI)(CI)M\" /T /Q", ignoreErrors: true);
    }

    private static void WarnIfHostingBundleMissing()
    {
        var ancmPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "IIS", "Asp.Net Core Module V2", "aspnetcorev2.dll");

        if (!File.Exists(ancmPath))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
            Console.WriteLine("WARNING: The ASP.NET Core Module (IIS Hosting Bundle) doesn't appear to be installed.");
            Console.WriteLine("The site will be created, but requests will fail with a 500.19/502.5 error until you");
            Console.WriteLine("install the .NET 8 Hosting Bundle from https://dotnet.microsoft.com/download/dotnet/8.0");
            Console.WriteLine("(look for 'Hosting Bundle' under ASP.NET Core Runtime 8.0.x) and run `iisreset`.");
            Console.WriteLine();
            Console.ResetColor();
        }
    }

    private static bool ObjectExists(string listArguments) => RunAppCmd(listArguments, ignoreErrors: true, silent: true) == 0;

    private static int RunAppCmd(string arguments, bool ignoreErrors = false, bool silent = false) =>
        RunProcess(AppCmdPath, arguments, ignoreErrors, silent);

    private static int RunProcess(string fileName, string arguments, bool ignoreErrors = false, bool silent = false)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start '{fileName}'.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!silent)
        {
            if (!string.IsNullOrWhiteSpace(stdout)) Console.WriteLine(stdout.TrimEnd());
            if (!string.IsNullOrWhiteSpace(stderr)) Console.Error.WriteLine(stderr.TrimEnd());
        }

        if (process.ExitCode != 0 && !ignoreErrors)
        {
            throw new InvalidOperationException($"'{fileName} {arguments}' failed (exit code {process.ExitCode}): {stderr}");
        }

        return process.ExitCode;
    }
}
