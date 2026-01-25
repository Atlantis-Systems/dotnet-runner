namespace Rot.Models;

/// <summary>
/// Represents a prompt configuration for interactive task execution.
/// </summary>
public class TaskPrompt
{
    /// <summary>
    /// The message to display to the user when prompting for input.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional default value if the user provides no input.
    /// </summary>
    public string? Default { get; set; }

    /// <summary>
    /// Optional list of choices to present to the user.
    /// When provided, the user must select from these options.
    /// </summary>
    public string[] Choices { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether the input should be masked (for sensitive values).
    /// </summary>
    public bool Secret { get; set; } = false;

    /// <summary>
    /// Whether the prompt is required (no empty input allowed).
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// Optional validation pattern (regex) for the input.
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// Error message to display when validation fails.
    /// </summary>
    public string? ValidationMessage { get; set; }
}
