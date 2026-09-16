using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace ATMTicketing.Deploy;

public static partial class SqlDeployer
{
    private static readonly string[] ScriptOrder =
    {
        "01_Schema.sql", "02_Indexes.sql", "03_StoredProcedures.sql", "04_Views.sql", "05_SeedData.sql"
    };

    public static void ProvisionDatabase(DeploymentOptions options)
    {
        ValidateIdentifier(options.Database);

        var masterConnectionString = BuildConnectionString(options, "master");
        Console.WriteLine($"Connecting to '{options.SqlServer}' ...");

        using (var connection = new SqlConnection(masterConnectionString))
        {
            connection.Open();

            var exists = ScalarBool(connection,
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.databases WHERE name = @name) THEN 1 ELSE 0 END",
                ("@name", options.Database));

            if (!exists)
            {
                Console.WriteLine($"Creating database [{options.Database}] ...");
                // CREATE DATABASE can't take the name as a parameter; the identifier is
                // validated by ValidateIdentifier() above and bracket-quoted here.
                using var create = new SqlCommand($"CREATE DATABASE [{options.Database}]", connection);
                create.ExecuteNonQuery();
            }
            else
            {
                Console.WriteLine($"Database [{options.Database}] already exists — reusing it.");
            }
        }

        var targetConnectionString = BuildConnectionString(options, options.Database);
        using (var connection = new SqlConnection(targetConnectionString))
        {
            connection.Open();

            foreach (var scriptName in ScriptOrder)
            {
                Console.WriteLine($"Applying {scriptName} ...");
                var script = ReadEmbeddedScript(scriptName);
                foreach (var batch in SplitOnGo(script))
                {
                    if (string.IsNullOrWhiteSpace(batch))
                    {
                        continue;
                    }
                    using var command = new SqlCommand(batch, connection) { CommandTimeout = 120 };
                    try
                    {
                        command.ExecuteNonQuery();
                    }
                    catch (SqlException ex) when (IsBenignAlreadyExistsError(ex))
                    {
                        // Scripts are safe to re-run: CREATE TABLE/SEQUENCE aren't guarded by
                        // IF NOT EXISTS the way the seed data is, so a second run against an
                        // already-provisioned database hits "already exists" here — skip it
                        // rather than aborting the whole deployment.
                        Console.WriteLine($"  (skipped: {ex.Message.Split('\n')[0]})");
                    }
                }
            }
        }

        Console.WriteLine("Database provisioning complete.");
    }

    private static bool IsBenignAlreadyExistsError(SqlException ex) =>
        ex.Errors.Cast<SqlError>().Any(e => e.Number is 2714 or 1913 or 15233 or 2705); // object/index/sequence/column already exists

    private static string BuildConnectionString(DeploymentOptions options, string database)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = options.SqlServer,
            InitialCatalog = database,
            TrustServerCertificate = true,
            ConnectTimeout = 30
        };

        if (options.UseSqlAuth)
        {
            builder.UserID = options.SqlUser;
            builder.Password = options.SqlPassword;
        }
        else
        {
            builder.IntegratedSecurity = true;
        }

        return builder.ConnectionString;
    }

    private static string ReadEmbeddedScript(string logicalName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"Embedded SQL script '{logicalName}' was not found in the installer.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Splits a .sql file into batches the way sqlcmd/SSMS do: on a line containing
    /// only the word GO (optionally followed by a repeat count), which is a client-side
    /// convention, not valid T-SQL, so it can't just be sent to the server as-is.</summary>
    private static IEnumerable<string> SplitOnGo(string script)
    {
        var lines = script.Replace("\r\n", "\n").Split('\n');
        var current = new System.Text.StringBuilder();

        foreach (var line in lines)
        {
            if (GoLineRegex().IsMatch(line))
            {
                yield return current.ToString();
                current.Clear();
            }
            else
            {
                current.AppendLine(line);
            }
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    private static bool ScalarBool(SqlConnection connection, string sql, params (string name, object value)[] parameters)
    {
        using var command = new SqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        var result = command.ExecuteScalar();
        return result is int i && i == 1;
    }

    private static void ValidateIdentifier(string name)
    {
        if (!SafeIdentifierRegex().IsMatch(name))
        {
            throw new ArgumentException(
                $"'{name}' isn't a safe SQL Server database name (letters, digits, underscores only, starting with a letter).");
        }
    }

    [GeneratedRegex(@"^\s*GO\s*(\d+)?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex GoLineRegex();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex SafeIdentifierRegex();
}
