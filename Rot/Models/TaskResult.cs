namespace Rot.Models;

/// <summary>
/// Represents the result of a task execution.
/// </summary>
public record TaskResult
{
    /// <summary>
    /// Whether the task completed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The exit code from the process (0 for success, non-zero for failure, -1 for timeout).
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// The name of the task that was executed.
    /// </summary>
    public string TaskName { get; init; } = string.Empty;

    /// <summary>
    /// How long the task took to execute.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Error message if the task failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Whether the task was skipped (due to cache or condition).
    /// </summary>
    public bool Skipped { get; init; }

    /// <summary>
    /// Reason for skipping, if applicable.
    /// </summary>
    public string? SkipReason { get; init; }

    /// <summary>
    /// Whether this was a dry run.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Captured standard output from the task.
    /// </summary>
    public string? StandardOutput { get; init; }

    /// <summary>
    /// Captured standard error from the task.
    /// </summary>
    public string? StandardError { get; init; }

    /// <summary>
    /// Combined output (stdout + stderr) from the task.
    /// </summary>
    public string? CombinedOutput { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static TaskResult Succeeded(string taskName, TimeSpan duration, string? stdout = null, string? stderr = null) => new()
    {
        Success = true,
        ExitCode = 0,
        TaskName = taskName,
        Duration = duration,
        StandardOutput = stdout,
        StandardError = stderr,
        CombinedOutput = CombineOutput(stdout, stderr)
    };

    private static string? CombineOutput(string? stdout, string? stderr)
    {
        if (string.IsNullOrEmpty(stdout) && string.IsNullOrEmpty(stderr))
            return null;
        if (string.IsNullOrEmpty(stderr))
            return stdout;
        if (string.IsNullOrEmpty(stdout))
            return stderr;
        return $"{stdout}\n{stderr}";
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static TaskResult Failed(string taskName, int exitCode, string? errorMessage = null, TimeSpan? duration = null) => new()
    {
        Success = false,
        ExitCode = exitCode,
        TaskName = taskName,
        ErrorMessage = errorMessage,
        Duration = duration ?? TimeSpan.Zero
    };

    /// <summary>
    /// Creates a skipped result.
    /// </summary>
    public static TaskResult SkippedResult(string taskName, string reason) => new()
    {
        Success = true,
        ExitCode = 0,
        TaskName = taskName,
        Skipped = true,
        SkipReason = reason
    };

    /// <summary>
    /// Creates a timeout result.
    /// </summary>
    public static TaskResult TimedOut(string taskName, int timeoutSeconds) => new()
    {
        Success = false,
        ExitCode = -1,
        TaskName = taskName,
        ErrorMessage = $"Task timed out after {timeoutSeconds} seconds"
    };

    /// <summary>
    /// Creates a dry run result.
    /// </summary>
    public static TaskResult DryRunResult(string taskName) => new()
    {
        Success = true,
        ExitCode = 0,
        TaskName = taskName,
        DryRun = true
    };

    /// <summary>
    /// Creates a not found result.
    /// </summary>
    public static TaskResult NotFound(string taskName) => new()
    {
        Success = false,
        ExitCode = 1,
        TaskName = taskName,
        ErrorMessage = $"Task '{taskName}' not found"
    };

    /// <summary>
    /// Creates a circular dependency result.
    /// </summary>
    public static TaskResult CircularDependency(string taskName) => new()
    {
        Success = false,
        ExitCode = 1,
        TaskName = taskName,
        ErrorMessage = $"Circular dependency detected for task '{taskName}'"
    };
}

/// <summary>
/// Represents the aggregated result of multiple task executions.
/// </summary>
public record TasksResult
{
    /// <summary>
    /// Whether all tasks completed successfully.
    /// </summary>
    public bool Success => Results.All(r => r.Success);

    /// <summary>
    /// The overall exit code (0 if all succeeded, otherwise first failure).
    /// </summary>
    public int ExitCode => Success ? 0 : Results.FirstOrDefault(r => !r.Success)?.ExitCode ?? 1;

    /// <summary>
    /// Individual task results.
    /// </summary>
    public IReadOnlyList<TaskResult> Results { get; init; } = Array.Empty<TaskResult>();

    /// <summary>
    /// Total duration of all tasks.
    /// </summary>
    public TimeSpan TotalDuration => TimeSpan.FromTicks(Results.Sum(r => r.Duration.Ticks));

    /// <summary>
    /// Number of tasks that succeeded.
    /// </summary>
    public int SucceededCount => Results.Count(r => r.Success && !r.Skipped);

    /// <summary>
    /// Number of tasks that failed.
    /// </summary>
    public int FailedCount => Results.Count(r => !r.Success);

    /// <summary>
    /// Number of tasks that were skipped.
    /// </summary>
    public int SkippedCount => Results.Count(r => r.Skipped);
}
