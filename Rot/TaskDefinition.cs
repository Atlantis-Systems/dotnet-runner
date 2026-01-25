namespace Rot.Models;

/// <summary>
/// Represents a task definition in a tasks.json or tasks.yaml file.
/// </summary>
public class TaskDefinition
{
    /// <summary>
    /// Human-readable label for the task.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Type of task: "shell" (default) or "process".
    /// </summary>
    public string Type { get; set; } = "shell";

    /// <summary>
    /// The command to execute.
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Additional arguments to pass to the command.
    /// </summary>
    public string[] Args { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Working directory for command execution.
    /// </summary>
    public string Cwd { get; set; } = string.Empty;

    /// <summary>
    /// Environment variables to set for the task.
    /// </summary>
    public Dictionary<string, string> Env { get; set; } = new();

    /// <summary>
    /// Shell to use for shell-type tasks.
    /// </summary>
    public string Shell { get; set; } = string.Empty;

    /// <summary>
    /// Whether to echo command output to console.
    /// </summary>
    public bool Echo { get; set; } = true;

    /// <summary>
    /// List of task names that must complete before this task runs.
    /// </summary>
    public string[] DependsOn { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether dependencies can run concurrently.
    /// </summary>
    public bool AllowConcurrent { get; set; } = false;

    /// <summary>
    /// Timeout in seconds. Task will be killed if it exceeds this duration.
    /// </summary>
    public int? Timeout { get; set; }

    /// <summary>
    /// Group name for organizing related tasks.
    /// </summary>
    public string Group { get; set; } = string.Empty;

    /// <summary>
    /// Tags for categorizing and filtering tasks.
    /// </summary>
    public string[] Tags { get; set; } = Array.Empty<string>();

    // Phase 3: Conditional execution
    /// <summary>
    /// Conditions that must be met for the task to execute.
    /// </summary>
    public TaskCondition? Condition { get; set; }

    // Phase 3: Task hooks
    /// <summary>
    /// Tasks to run before this task executes.
    /// </summary>
    public string[] PreTasks { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Tasks to run after this task completes successfully.
    /// </summary>
    public string[] PostTasks { get; set; } = Array.Empty<string>();

    // Phase 3: Task caching
    /// <summary>
    /// Cache configuration for skipping execution when inputs haven't changed.
    /// </summary>
    public TaskCacheConfig? Cache { get; set; }

    // Phase 6: Interactive prompts
    /// <summary>
    /// Prompts to collect user input before task execution.
    /// The key is the variable name, the value is the prompt configuration.
    /// </summary>
    public Dictionary<string, TaskPrompt> Prompts { get; set; } = new();

    // Phase 6: Fine-grained parallelism control
    /// <summary>
    /// Maximum number of dependencies to run in parallel.
    /// When set, limits concurrent dependency execution regardless of AllowConcurrent.
    /// </summary>
    public int? Parallel { get; set; }
}

