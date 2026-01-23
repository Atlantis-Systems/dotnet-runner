namespace Rot.Models;

/// <summary>
/// Defines a profile configuration that can override variables and environment settings.
/// </summary>
public class TaskProfile
{
    /// <summary>
    /// Variables to override or add when this profile is active.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = new();

    /// <summary>
    /// Environment variables to set when this profile is active.
    /// </summary>
    public Dictionary<string, string> Env { get; set; } = new();
}
