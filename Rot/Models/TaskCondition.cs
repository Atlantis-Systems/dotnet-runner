namespace Rot.Models;

/// <summary>
/// Defines conditions that must be met for a task to execute.
/// </summary>
public class TaskCondition
{
    /// <summary>
    /// Operating system condition (windows, linux, osx/macos).
    /// </summary>
    public string? Os { get; set; }

    /// <summary>
    /// Environment variable conditions. All must match for the condition to be true.
    /// Format: {"VAR_NAME": "expected_value"} or {"VAR_NAME": ""} to check existence.
    /// </summary>
    public Dictionary<string, string> Env { get; set; } = new();

    /// <summary>
    /// Files that must exist for the condition to be true.
    /// </summary>
    public string[] FileExists { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Files that must NOT exist for the condition to be true.
    /// </summary>
    public string[] FileNotExists { get; set; } = Array.Empty<string>();
}
