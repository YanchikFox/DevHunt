using System.IO;
using System.Text.RegularExpressions;

namespace DevHunt.Infrastructure.Configuration;

/// <summary>
/// Lightweight .env loader so local tooling (dotnet ef, console apps) can use the same
/// environment variables as Docker Compose without duplicating secrets in appsettings.
/// </summary>
public static class EnvLoader
{
    private static readonly object SyncRoot = new();
    private static bool _loaded;

    public static void Load(string? customPath = null)
    {
        if (_loaded) return;

        lock (SyncRoot)
        {
            if (_loaded) return;

            var envPath = customPath ?? FindEnvFile();
            if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
            {
                LoadFile(envPath);
            }

            ApplyLocalDatabaseOverrides();

            _loaded = true;
        }
    }

    private static string? FindEnvFile()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            var candidate = Path.Combine(current, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(current);
            if (parent == null)
            {
                break;
            }
            current = parent.FullName;
        }

        return null;
    }

    public static string? BuildConnectionStringFromPostgresEnv(string? hostOverride = null)
    {
        var host = hostOverride ?? Environment.GetEnvironmentVariable("POSTGRES_HOST");
        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("POSTGRES_DB");
        var username = Environment.GetEnvironmentVariable("POSTGRES_USER");
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");

        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(database) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
    }

    private static void LoadFile(string path)
    {
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            var value = line[(separatorIndex + 1)..].Trim();
            value = StripQuotes(value);
            value = ExpandVariables(value);

            var existing = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrEmpty(existing))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static void ApplyLocalDatabaseOverrides()
    {
        var runningInContainer = string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (runningInContainer)
        {
            return;
        }

        var serviceName = Environment.GetEnvironmentVariable("SERVICE_DB_NAME");
        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST");

        if (string.IsNullOrWhiteSpace(serviceName) ||
            string.IsNullOrWhiteSpace(host) ||
            !string.Equals(host, serviceName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var localConnection = BuildConnectionStringFromPostgresEnv("localhost");
        if (string.IsNullOrWhiteSpace(localConnection))
        {
            return;
        }

        Environment.SetEnvironmentVariable("POSTGRES_HOST", "localhost");
        SetConnectionStringIfMatches("CONNECTIONSTRINGS__DEFAULTCONNECTION", serviceName, localConnection);
        SetConnectionStringIfMatches("CONNECTIONSTRINGS__READONLYCONNECTION", serviceName, localConnection);
        Environment.SetEnvironmentVariable("DEVHUNT_DB_CONNECTION", localConnection);
    }

    private static void SetConnectionStringIfMatches(string key, string serviceName, string newValue)
    {
        var existing = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(existing))
        {
            Environment.SetEnvironmentVariable(key, newValue);
            return;
        }

        if (existing.Contains($"Host={serviceName}", StringComparison.OrdinalIgnoreCase))
        {
            Environment.SetEnvironmentVariable(key, newValue);
        }
    }

    private static string StripQuotes(string value)
    {
        if (value.Length >= 2 &&
            ((value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal)) ||
             (value.StartsWith("'", StringComparison.Ordinal) && value.EndsWith("'", StringComparison.Ordinal))))
        {
            return value[1..^1];
        }

        return value;
    }

    private static string ExpandVariables(string value)
    {
        return Regex.Replace(value, @"\$\{([^}:]+)(?::-[^}]*)?\}", match =>
        {
            var variable = match.Groups[1].Value;
            var envValue = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrEmpty(envValue))
            {
                return envValue;
            }

            var defaultValueGroup = match.Value.Split(":-", StringSplitOptions.RemoveEmptyEntries);
            if (defaultValueGroup.Length == 2)
            {
                return defaultValueGroup[1].TrimEnd('}');
            }

            return string.Empty;
        });
    }
}
