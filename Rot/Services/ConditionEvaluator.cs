using Rot.Logging;
using Rot.Models;

namespace Rot.Services;

/// <summary>
/// Evaluates task conditions to determine if a task should execute.
/// </summary>
public class ConditionEvaluator
{
    private readonly ITaskLogger _logger;
    private readonly string _workingDirectory;

    public ConditionEvaluator(ITaskLogger logger, string? workingDirectory = null)
    {
        _logger = logger;
        _workingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Evaluates whether all conditions are met for task execution.
    /// </summary>
    /// <param name="condition">The condition to evaluate.</param>
    /// <param name="taskName">Task name for logging purposes.</param>
    /// <returns>True if all conditions are met, false otherwise.</returns>
    public bool Evaluate(TaskCondition? condition, string taskName)
    {
        if (condition == null)
        {
            return true;
        }

        // Check OS condition
        if (!string.IsNullOrEmpty(condition.Os))
        {
            if (!EvaluateOsCondition(condition.Os))
            {
                _logger.Info("Task '{TaskName}' skipped: OS condition not met (requires '{Os}')", taskName, condition.Os);
                return false;
            }
        }

        // Check environment variable conditions
        if (condition.Env.Count > 0)
        {
            foreach (var (envVar, expectedValue) in condition.Env)
            {
                if (!EvaluateEnvCondition(envVar, expectedValue))
                {
                    _logger.Info("Task '{TaskName}' skipped: Environment condition not met ({EnvVar}={ExpectedValue})", taskName, envVar, expectedValue);
                    return false;
                }
            }
        }

        // Check file exists conditions
        if (condition.FileExists.Length > 0)
        {
            foreach (var filePath in condition.FileExists)
            {
                var fullPath = GetFullPath(filePath);
                if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                {
                    _logger.Info("Task '{TaskName}' skipped: Required file/directory not found '{FilePath}'", taskName, filePath);
                    return false;
                }
            }
        }

        // Check file not exists conditions
        if (condition.FileNotExists.Length > 0)
        {
            foreach (var filePath in condition.FileNotExists)
            {
                var fullPath = GetFullPath(filePath);
                if (File.Exists(fullPath) || Directory.Exists(fullPath))
                {
                    _logger.Info("Task '{TaskName}' skipped: File/directory should not exist '{FilePath}'", taskName, filePath);
                    return false;
                }
            }
        }

        _logger.Debug("Task '{TaskName}' conditions met", taskName);
        return true;
    }

    private bool EvaluateOsCondition(string os)
    {
        var normalizedOs = os.ToLowerInvariant().Trim();

        return normalizedOs switch
        {
            "windows" or "win" => OperatingSystem.IsWindows(),
            "linux" => OperatingSystem.IsLinux(),
            "osx" or "macos" or "mac" => OperatingSystem.IsMacOS(),
            "unix" => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS(),
            _ => false
        };
    }

    private bool EvaluateEnvCondition(string envVar, string expectedValue)
    {
        var actualValue = Environment.GetEnvironmentVariable(envVar);

        // If expected value is empty, just check if the variable exists
        if (string.IsNullOrEmpty(expectedValue))
        {
            return actualValue != null;
        }

        // Check if the value matches
        return string.Equals(actualValue, expectedValue, StringComparison.Ordinal);
    }

    private string GetFullPath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.Combine(_workingDirectory, path);
    }
}
