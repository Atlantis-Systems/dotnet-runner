using System.Text.Json;
using Rot.Logging;
using Rot.Models;

namespace Rot.Services;

/// <summary>
/// Audit logger for tracking all task executions for compliance and security.
/// </summary>
public class AuditLogger : IDisposable
{
    private readonly string _auditFilePath;
    private readonly ITaskLogger _logger;
    private readonly bool _enabled;
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new AuditLogger instance.
    /// </summary>
    /// <param name="auditFilePath">Path to the audit log file. If null, uses default location.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="enabled">Whether audit logging is enabled.</param>
    public AuditLogger(string? auditFilePath, ITaskLogger logger, bool enabled = true)
    {
        _logger = logger;
        _enabled = enabled;

        if (enabled)
        {
            _auditFilePath = auditFilePath ?? GetDefaultAuditFilePath();
            EnsureAuditDirectory();
            _logger.Debug("Audit logging enabled at: {AuditFilePath}", _auditFilePath);
        }
        else
        {
            _auditFilePath = string.Empty;
        }
    }

    /// <summary>
    /// Gets the default audit file path.
    /// </summary>
    private static string GetDefaultAuditFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var rotDir = Path.Combine(appData, "rot");
        return Path.Combine(rotDir, "audit.log");
    }

    /// <summary>
    /// Ensures the audit log directory exists.
    /// </summary>
    private void EnsureAuditDirectory()
    {
        var dir = Path.GetDirectoryName(_auditFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    /// <summary>
    /// Logs a task execution start event.
    /// </summary>
    public void LogTaskStart(string taskName, string command, string? workingDirectory = null)
    {
        if (!_enabled) return;

        var entry = new AuditEntry
        {
            EventType = AuditEventType.TaskStart,
            TaskName = taskName,
            Command = SanitizeCommand(command),
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            Timestamp = DateTime.UtcNow,
            User = GetCurrentUser(),
            Machine = Environment.MachineName,
            ProcessId = Environment.ProcessId
        };

        WriteEntry(entry);
    }

    /// <summary>
    /// Logs a task execution completion event.
    /// </summary>
    public void LogTaskComplete(string taskName, int exitCode, TimeSpan duration, bool success)
    {
        if (!_enabled) return;

        var entry = new AuditEntry
        {
            EventType = AuditEventType.TaskComplete,
            TaskName = taskName,
            ExitCode = exitCode,
            DurationMs = (long)duration.TotalMilliseconds,
            Success = success,
            Timestamp = DateTime.UtcNow,
            User = GetCurrentUser(),
            Machine = Environment.MachineName,
            ProcessId = Environment.ProcessId
        };

        WriteEntry(entry);
    }

    /// <summary>
    /// Logs a task execution failure event.
    /// </summary>
    public void LogTaskFailed(string taskName, int exitCode, string? errorMessage, TimeSpan duration)
    {
        if (!_enabled) return;

        var entry = new AuditEntry
        {
            EventType = AuditEventType.TaskFailed,
            TaskName = taskName,
            ExitCode = exitCode,
            ErrorMessage = errorMessage,
            DurationMs = (long)duration.TotalMilliseconds,
            Success = false,
            Timestamp = DateTime.UtcNow,
            User = GetCurrentUser(),
            Machine = Environment.MachineName,
            ProcessId = Environment.ProcessId
        };

        WriteEntry(entry);
    }

    /// <summary>
    /// Logs a task timeout event.
    /// </summary>
    public void LogTaskTimeout(string taskName, int timeoutSeconds)
    {
        if (!_enabled) return;

        var entry = new AuditEntry
        {
            EventType = AuditEventType.TaskTimeout,
            TaskName = taskName,
            ErrorMessage = $"Task timed out after {timeoutSeconds} seconds",
            TimeoutSeconds = timeoutSeconds,
            Success = false,
            Timestamp = DateTime.UtcNow,
            User = GetCurrentUser(),
            Machine = Environment.MachineName,
            ProcessId = Environment.ProcessId
        };

        WriteEntry(entry);
    }

    /// <summary>
    /// Logs a task skip event (cached or condition not met).
    /// </summary>
    public void LogTaskSkipped(string taskName, string reason)
    {
        if (!_enabled) return;

        var entry = new AuditEntry
        {
            EventType = AuditEventType.TaskSkipped,
            TaskName = taskName,
            SkipReason = reason,
            Success = true,
            Timestamp = DateTime.UtcNow,
            User = GetCurrentUser(),
            Machine = Environment.MachineName,
            ProcessId = Environment.ProcessId
        };

        WriteEntry(entry);
    }

    /// <summary>
    /// Logs a security validation failure.
    /// </summary>
    public void LogSecurityViolation(string taskName, string command, IEnumerable<string> violations)
    {
        if (!_enabled) return;

        var entry = new AuditEntry
        {
            EventType = AuditEventType.SecurityViolation,
            TaskName = taskName,
            Command = SanitizeCommand(command),
            ErrorMessage = string.Join("; ", violations),
            Success = false,
            Timestamp = DateTime.UtcNow,
            User = GetCurrentUser(),
            Machine = Environment.MachineName,
            ProcessId = Environment.ProcessId
        };

        WriteEntry(entry);
        _logger.Warning("Security violation logged for task '{TaskName}'", taskName);
    }

    /// <summary>
    /// Writes an audit entry to the log file.
    /// </summary>
    private void WriteEntry(AuditEntry entry)
    {
        try
        {
            var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            lock (_lock)
            {
                File.AppendAllText(_auditFilePath, json + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning("Failed to write audit log entry: {Error}", ex.Message);
        }
    }

    /// <summary>
    /// Gets the current user name.
    /// </summary>
    private static string GetCurrentUser()
    {
        return Environment.UserName;
    }

    /// <summary>
    /// Sanitizes command for logging (removes potential secrets).
    /// </summary>
    private static string SanitizeCommand(string command)
    {
        // Replace potential secret values in common patterns
        var sanitized = command;

        // Mask values after common secret keywords
        var secretPatterns = new[]
        {
            @"(password[=:\s]+)([^\s""']+)",
            @"(secret[=:\s]+)([^\s""']+)",
            @"(token[=:\s]+)([^\s""']+)",
            @"(api[_-]?key[=:\s]+)([^\s""']+)",
            @"(auth[=:\s]+)([^\s""']+)"
        };

        foreach (var pattern in secretPatterns)
        {
            sanitized = System.Text.RegularExpressions.Regex.Replace(
                sanitized,
                pattern,
                "$1***",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return sanitized;
    }

    /// <summary>
    /// Gets recent audit entries.
    /// </summary>
    /// <param name="count">Maximum number of entries to return.</param>
    /// <returns>List of recent audit entries.</returns>
    public List<AuditEntry> GetRecentEntries(int count = 100)
    {
        var entries = new List<AuditEntry>();

        if (!_enabled || !File.Exists(_auditFilePath))
            return entries;

        try
        {
            var lines = File.ReadAllLines(_auditFilePath);
            var start = Math.Max(0, lines.Length - count);

            for (int i = start; i < lines.Length; i++)
            {
                try
                {
                    var entry = JsonSerializer.Deserialize<AuditEntry>(lines[i], new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (entry != null)
                        entries.Add(entry);
                }
                catch
                {
                    // Skip malformed entries
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Warning("Failed to read audit log: {Error}", ex.Message);
        }

        return entries;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// Types of audit events.
/// </summary>
public enum AuditEventType
{
    TaskStart,
    TaskComplete,
    TaskFailed,
    TaskTimeout,
    TaskSkipped,
    SecurityViolation
}

/// <summary>
/// Represents an audit log entry.
/// </summary>
public class AuditEntry
{
    /// <summary>
    /// Type of audit event.
    /// </summary>
    public AuditEventType EventType { get; set; }

    /// <summary>
    /// Name of the task.
    /// </summary>
    public string TaskName { get; set; } = string.Empty;

    /// <summary>
    /// Command that was executed (sanitized).
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// Working directory for the command.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Exit code of the command.
    /// </summary>
    public int? ExitCode { get; set; }

    /// <summary>
    /// Duration of execution in milliseconds.
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Whether the task completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the task failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Reason the task was skipped.
    /// </summary>
    public string? SkipReason { get; set; }

    /// <summary>
    /// Timeout value in seconds.
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// UTC timestamp of the event.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// User who executed the task.
    /// </summary>
    public string User { get; set; } = string.Empty;

    /// <summary>
    /// Machine name where the task was executed.
    /// </summary>
    public string Machine { get; set; } = string.Empty;

    /// <summary>
    /// Process ID of the rot process.
    /// </summary>
    public int ProcessId { get; set; }
}
