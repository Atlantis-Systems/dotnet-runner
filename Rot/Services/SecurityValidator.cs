using Rot.Logging;

namespace Rot.Services;

/// <summary>
/// Provides security validation for task execution.
/// </summary>
public class SecurityValidator
{
    private readonly ITaskLogger _logger;
    private readonly string? _projectRoot;

    // Dangerous shell metacharacters that could allow injection
    private static readonly char[] DangerousEnvChars = { ';', '|', '&', '`', '\n', '\r', '$' };

    // Dangerous command patterns that should be blocked
    private static readonly string[] DangerousPatterns =
    {
        "rm -rf /",
        "rm -rf /*",
        ":(){ :|:& };:",  // Fork bomb
        "dd if=/dev/zero",
        "dd if=/dev/random",
        "> /dev/sda",
        "mkfs.",
        "chmod -R 777 /",
        "wget.*|.*sh",
        "curl.*|.*sh",
        "> /etc/passwd",
        "> /etc/shadow"
    };

    // Allowed environment variable patterns (safe patterns)
    private static readonly string[] SafeEnvPatterns =
    {
        "PATH",
        "HOME",
        "USER",
        "SHELL",
        "TERM",
        "LANG",
        "LC_",
        "TZ",
        "DOTNET_",
        "ASPNETCORE_",
        "NODE_",
        "NPM_",
        "JAVA_",
        "MAVEN_",
        "GRADLE_",
        "PYTHON",
        "VIRTUAL_ENV",
        "CI",
        "BUILD_",
        "GITHUB_",
        "DEBUG",
        "VERBOSE",
        "LOG_"
    };

    public SecurityValidator(ITaskLogger? logger = null, string? projectRoot = null)
    {
        _logger = logger ?? NullLogger.Instance;
        _projectRoot = projectRoot ?? Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Validates an environment variable value for potential shell injection.
    /// </summary>
    /// <param name="name">The environment variable name.</param>
    /// <param name="value">The environment variable value.</param>
    /// <returns>A validation result indicating if the value is safe.</returns>
    public ValidationResult ValidateEnvValue(string name, string value)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrEmpty(value))
            return new ValidationResult(errors, warnings);

        // Check for dangerous characters
        foreach (var c in DangerousEnvChars)
        {
            if (value.Contains(c))
            {
                // Allow $ in some well-known safe patterns
                if (c == '$' && IsSafeVariableReference(value))
                {
                    warnings.Add($"Environment variable '{name}' contains variable reference: {value}");
                    continue;
                }

                // Newlines in multi-line values might be intentional
                if (c == '\n' || c == '\r')
                {
                    warnings.Add($"Environment variable '{name}' contains newline characters");
                    continue;
                }

                errors.Add($"Environment variable '{name}' contains potentially dangerous character '{c}': {TruncateValue(value)}");
            }
        }

        return new ValidationResult(errors, warnings);
    }

    /// <summary>
    /// Validates a command for potentially dangerous patterns.
    /// </summary>
    /// <param name="command">The command to validate.</param>
    /// <returns>A validation result indicating if the command is safe.</returns>
    public ValidationResult ValidateCommand(string command)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrEmpty(command))
        {
            errors.Add("Command cannot be empty");
            return new ValidationResult(errors, warnings);
        }

        var normalizedCommand = command.ToLowerInvariant().Replace("\\", "/");

        foreach (var pattern in DangerousPatterns)
        {
            if (normalizedCommand.Contains(pattern.ToLowerInvariant()))
            {
                errors.Add($"Command contains dangerous pattern '{pattern}': {TruncateValue(command)}");
            }
        }

        // Warn about potentially risky but not necessarily dangerous patterns
        if (normalizedCommand.Contains("sudo "))
        {
            warnings.Add($"Command uses 'sudo' which requires elevated privileges");
        }

        if (normalizedCommand.Contains("rm -rf"))
        {
            warnings.Add($"Command uses 'rm -rf' which permanently deletes files");
        }

        if (normalizedCommand.Contains("eval ") || normalizedCommand.Contains("$("))
        {
            warnings.Add($"Command uses dynamic evaluation which could be a security risk");
        }

        return new ValidationResult(errors, warnings);
    }

    /// <summary>
    /// Validates a working directory is within expected project boundaries.
    /// </summary>
    /// <param name="cwd">The working directory path.</param>
    /// <returns>A validation result indicating if the path is safe.</returns>
    public ValidationResult ValidateWorkingDirectory(string cwd)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrEmpty(cwd))
            return new ValidationResult(errors, warnings);

        try
        {
            var fullPath = Path.GetFullPath(cwd);

            // Check if the path tries to escape the project root
            if (!string.IsNullOrEmpty(_projectRoot))
            {
                var projectRootFull = Path.GetFullPath(_projectRoot);

                if (!fullPath.StartsWith(projectRootFull, StringComparison.OrdinalIgnoreCase))
                {
                    // Allow parent directories up to a reasonable level
                    var relativePath = Path.GetRelativePath(projectRootFull, fullPath);
                    var parentLevels = relativePath.Split(Path.DirectorySeparatorChar)
                        .Count(p => p == "..");

                    if (parentLevels > 3)
                    {
                        errors.Add($"Working directory '{cwd}' is too far outside project root");
                    }
                    else if (parentLevels > 0)
                    {
                        warnings.Add($"Working directory '{cwd}' is outside project root");
                    }
                }
            }

            // Check for suspicious paths
            var normalizedPath = fullPath.Replace("\\", "/").ToLowerInvariant();
            var suspiciousPaths = new[] { "/etc", "/var", "/usr", "/root", "/home", "/tmp" };

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                foreach (var suspicious in suspiciousPaths)
                {
                    if (normalizedPath.StartsWith(suspicious) && !normalizedPath.StartsWith("/home/" + Environment.UserName))
                    {
                        warnings.Add($"Working directory '{cwd}' is in a system location");
                        break;
                    }
                }
            }

            // Check on Windows for system directories
            if (OperatingSystem.IsWindows())
            {
                var windowsSuspicious = new[] { "c:/windows", "c:/program files", "c:/programdata" };
                foreach (var suspicious in windowsSuspicious)
                {
                    if (normalizedPath.StartsWith(suspicious))
                    {
                        warnings.Add($"Working directory '{cwd}' is in a system location");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Invalid working directory path '{cwd}': {ex.Message}");
        }

        return new ValidationResult(errors, warnings);
    }

    /// <summary>
    /// Validates all environment variables in a dictionary.
    /// </summary>
    public ValidationResult ValidateEnvironmentVariables(Dictionary<string, string> env)
    {
        var allErrors = new List<string>();
        var allWarnings = new List<string>();

        foreach (var (name, value) in env)
        {
            var result = ValidateEnvValue(name, value);
            allErrors.AddRange(result.Errors);
            allWarnings.AddRange(result.Warnings);
        }

        return new ValidationResult(allErrors, allWarnings);
    }

    /// <summary>
    /// Performs full security validation on a task.
    /// </summary>
    public ValidationResult ValidateTask(string taskName, string command, string? cwd, Dictionary<string, string>? env)
    {
        var allErrors = new List<string>();
        var allWarnings = new List<string>();

        // Validate command
        var cmdResult = ValidateCommand(command);
        allErrors.AddRange(cmdResult.Errors.Select(e => $"[{taskName}] {e}"));
        allWarnings.AddRange(cmdResult.Warnings.Select(w => $"[{taskName}] {w}"));

        // Validate working directory
        if (!string.IsNullOrEmpty(cwd))
        {
            var cwdResult = ValidateWorkingDirectory(cwd);
            allErrors.AddRange(cwdResult.Errors.Select(e => $"[{taskName}] {e}"));
            allWarnings.AddRange(cwdResult.Warnings.Select(w => $"[{taskName}] {w}"));
        }

        // Validate environment variables
        if (env != null && env.Count > 0)
        {
            var envResult = ValidateEnvironmentVariables(env);
            allErrors.AddRange(envResult.Errors.Select(e => $"[{taskName}] {e}"));
            allWarnings.AddRange(envResult.Warnings.Select(w => $"[{taskName}] {w}"));
        }

        // Log warnings
        foreach (var warning in allWarnings)
        {
            _logger.Warning(warning);
        }

        // Log errors
        foreach (var error in allErrors)
        {
            _logger.Error(error);
        }

        return new ValidationResult(allErrors, allWarnings);
    }

    private bool IsSafeVariableReference(string value)
    {
        // Check if $ is used in safe patterns like ${VAR} or $VAR
        // where VAR is a known safe variable name pattern
        foreach (var pattern in SafeEnvPatterns)
        {
            if (value.Contains($"${{{pattern}") || value.Contains($"${pattern}"))
                return true;
        }

        // Also allow ${env:...} and ${...} syntax used by Rot
        if (value.Contains("${env:") || System.Text.RegularExpressions.Regex.IsMatch(value, @"\$\{[a-zA-Z_][a-zA-Z0-9_]*\}"))
            return true;

        return false;
    }

    private static string TruncateValue(string value, int maxLength = 50)
    {
        if (value.Length <= maxLength)
            return value;
        return value.Substring(0, maxLength) + "...";
    }
}

/// <summary>
/// Represents the result of a security validation.
/// </summary>
public record ValidationResult(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
    public bool HasWarnings => Warnings.Count > 0;

    public ValidationResult(List<string> errors) : this(errors, Array.Empty<string>()) { }

    public ValidationResult(List<string> errors, List<string> warnings) : this((IReadOnlyList<string>)errors, (IReadOnlyList<string>)warnings) { }
}
