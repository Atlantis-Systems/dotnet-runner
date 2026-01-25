using Rot.Logging;

namespace Rot.Services;

/// <summary>
/// Provider for loading secrets from various secure sources.
/// </summary>
public class SecretsProvider
{
    private readonly ITaskLogger _logger;
    private readonly Dictionary<string, ISecretsSource> _sources = new();

    /// <summary>
    /// Creates a new SecretsProvider with default sources.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    public SecretsProvider(ITaskLogger logger)
    {
        _logger = logger;

        // Register default sources
        RegisterSource("env", new EnvironmentSecretsSource());
        RegisterSource("file", new FileSecretsSource(logger));
        RegisterSource("dotenv", new DotEnvSecretsSource(logger));
    }

    /// <summary>
    /// Registers a custom secrets source.
    /// </summary>
    /// <param name="name">Source name (used in ${secret:source:key} syntax).</param>
    /// <param name="source">The secrets source implementation.</param>
    public void RegisterSource(string name, ISecretsSource source)
    {
        _sources[name.ToLowerInvariant()] = source;
    }

    /// <summary>
    /// Gets a secret value by key.
    /// </summary>
    /// <param name="key">Secret key in format "source:key" or just "key" (defaults to env source).</param>
    /// <returns>The secret value or null if not found.</returns>
    public string? GetSecret(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        // Parse key format: "source:key" or just "key"
        string sourceName;
        string secretKey;

        var colonIndex = key.IndexOf(':');
        if (colonIndex > 0)
        {
            sourceName = key.Substring(0, colonIndex).ToLowerInvariant();
            secretKey = key.Substring(colonIndex + 1);
        }
        else
        {
            // Default to environment source
            sourceName = "env";
            secretKey = key;
        }

        if (!_sources.TryGetValue(sourceName, out var source))
        {
            _logger.Warning("Unknown secrets source '{Source}' for key '{Key}'", sourceName, key);
            return null;
        }

        var value = source.GetSecret(secretKey);

        if (value == null)
        {
            _logger.Debug("Secret '{Key}' not found in source '{Source}'", secretKey, sourceName);
        }
        else
        {
            _logger.Debug("Secret '{Key}' loaded from source '{Source}'", secretKey, sourceName);
        }

        return value;
    }

    /// <summary>
    /// Checks if a secret exists.
    /// </summary>
    public bool HasSecret(string key)
    {
        return GetSecret(key) != null;
    }
}

/// <summary>
/// Interface for secrets source implementations.
/// </summary>
public interface ISecretsSource
{
    /// <summary>
    /// Gets a secret value by key.
    /// </summary>
    /// <param name="key">The secret key.</param>
    /// <returns>The secret value or null if not found.</returns>
    string? GetSecret(string key);
}

/// <summary>
/// Loads secrets from environment variables.
/// </summary>
public class EnvironmentSecretsSource : ISecretsSource
{
    public string? GetSecret(string key)
    {
        return Environment.GetEnvironmentVariable(key);
    }
}

/// <summary>
/// Loads secrets from a file.
/// Supports files containing a single secret value or key=value pairs.
/// </summary>
public class FileSecretsSource : ISecretsSource
{
    private readonly ITaskLogger _logger;
    private readonly Dictionary<string, string> _cache = new();

    public FileSecretsSource(ITaskLogger logger)
    {
        _logger = logger;
    }

    public string? GetSecret(string key)
    {
        // Check cache first
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        // Key format: filepath or filepath#keyname
        var hashIndex = key.IndexOf('#');
        string filePath;
        string? keyName = null;

        if (hashIndex > 0)
        {
            filePath = key.Substring(0, hashIndex);
            keyName = key.Substring(hashIndex + 1);
        }
        else
        {
            filePath = key;
        }

        // Expand environment variables in path
        filePath = Environment.ExpandEnvironmentVariables(filePath);

        if (!File.Exists(filePath))
        {
            _logger.Debug("Secret file not found: {FilePath}", filePath);
            return null;
        }

        try
        {
            var content = File.ReadAllText(filePath).Trim();

            if (keyName != null)
            {
                // Parse as key=value file
                var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith('#') || !trimmed.Contains('='))
                        continue;

                    var eqIndex = trimmed.IndexOf('=');
                    var lineKey = trimmed.Substring(0, eqIndex).Trim();
                    var lineValue = trimmed.Substring(eqIndex + 1).Trim();

                    if (lineKey.Equals(keyName, StringComparison.OrdinalIgnoreCase))
                    {
                        _cache[key] = lineValue;
                        return lineValue;
                    }
                }
                return null;
            }
            else
            {
                // File contains single secret
                _cache[key] = content;
                return content;
            }
        }
        catch (Exception ex)
        {
            _logger.Warning("Error reading secret file '{FilePath}': {Error}", filePath, ex.Message);
            return null;
        }
    }
}

/// <summary>
/// Loads secrets from .env files in the current or parent directories.
/// </summary>
public class DotEnvSecretsSource : ISecretsSource
{
    private readonly ITaskLogger _logger;
    private readonly Dictionary<string, string>? _envVars;

    public DotEnvSecretsSource(ITaskLogger logger)
    {
        _logger = logger;
        _envVars = LoadDotEnv();
    }

    private Dictionary<string, string>? LoadDotEnv()
    {
        // Search for .env file in current and parent directories
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var envFile = Path.Combine(dir, ".env");
            if (File.Exists(envFile))
            {
                return ParseDotEnv(envFile);
            }

            var secretsEnvFile = Path.Combine(dir, ".env.secrets");
            if (File.Exists(secretsEnvFile))
            {
                return ParseDotEnv(secretsEnvFile);
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    private Dictionary<string, string> ParseDotEnv(string filePath)
    {
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Skip comments and empty lines
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                    continue;

                var eqIndex = trimmed.IndexOf('=');
                if (eqIndex <= 0)
                    continue;

                var key = trimmed.Substring(0, eqIndex).Trim();
                var value = trimmed.Substring(eqIndex + 1).Trim();

                // Remove quotes if present
                if ((value.StartsWith('"') && value.EndsWith('"')) ||
                    (value.StartsWith('\'') && value.EndsWith('\'')))
                {
                    value = value.Substring(1, value.Length - 2);
                }

                vars[key] = value;
            }

            _logger.Debug("Loaded {Count} secrets from {FilePath}", vars.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger.Warning("Error reading .env file '{FilePath}': {Error}", filePath, ex.Message);
        }

        return vars;
    }

    public string? GetSecret(string key)
    {
        if (_envVars == null)
            return null;

        return _envVars.TryGetValue(key, out var value) ? value : null;
    }
}
