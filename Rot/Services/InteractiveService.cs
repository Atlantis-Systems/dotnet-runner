using System.Text.RegularExpressions;
using Rot.Logging;
using Rot.Models;

namespace Rot.Services;

/// <summary>
/// Service for handling interactive user prompts during task execution.
/// </summary>
public class InteractiveService
{
    private readonly ITaskLogger _logger;
    private readonly bool _nonInteractive;

    /// <summary>
    /// Creates a new InteractiveService instance.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="nonInteractive">When true, uses default values without prompting.</param>
    public InteractiveService(ITaskLogger logger, bool nonInteractive = false)
    {
        _logger = logger;
        _nonInteractive = nonInteractive;
    }

    /// <summary>
    /// Collects all prompt values for a task.
    /// </summary>
    /// <param name="taskName">Name of the task being executed.</param>
    /// <param name="prompts">Dictionary of prompt configurations.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary of collected variable values.</returns>
    public async Task<Dictionary<string, string>> CollectPromptsAsync(
        string taskName,
        Dictionary<string, TaskPrompt> prompts,
        CancellationToken ct = default)
    {
        var values = new Dictionary<string, string>();

        if (prompts.Count == 0)
            return values;

        _logger.Debug("Collecting {Count} prompts for task '{TaskName}'", prompts.Count, taskName);

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Task '{taskName}' requires input:");
        Console.ResetColor();
        Console.WriteLine();

        foreach (var (variableName, prompt) in prompts)
        {
            ct.ThrowIfCancellationRequested();

            var value = await PromptForValueAsync(variableName, prompt, ct);
            values[variableName] = value;
        }

        Console.WriteLine();
        return values;
    }

    /// <summary>
    /// Prompts for a single value.
    /// </summary>
    private async Task<string> PromptForValueAsync(string variableName, TaskPrompt prompt, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            // Build prompt message
            var message = string.IsNullOrEmpty(prompt.Message)
                ? $"Enter value for '{variableName}'"
                : prompt.Message;

            // Show choices if available
            if (prompt.Choices.Length > 0)
            {
                Console.WriteLine($"  {message}");
                for (int i = 0; i < prompt.Choices.Length; i++)
                {
                    var isDefault = prompt.Choices[i] == prompt.Default;
                    var defaultMarker = isDefault ? " (default)" : "";
                    Console.WriteLine($"    [{i + 1}] {prompt.Choices[i]}{defaultMarker}");
                }
                Console.Write("  Choice: ");
            }
            else
            {
                var defaultHint = !string.IsNullOrEmpty(prompt.Default) ? $" [{prompt.Default}]" : "";
                Console.Write($"  {message}{defaultHint}: ");
            }

            // Handle non-interactive mode
            if (_nonInteractive)
            {
                var defaultValue = prompt.Default ?? string.Empty;
                Console.WriteLine(prompt.Secret ? "***" : defaultValue);
                _logger.Debug("Non-interactive mode: using default value for '{VariableName}'", variableName);
                return defaultValue;
            }

            // Read input
            string? input;
            if (prompt.Secret)
            {
                input = await ReadSecretAsync(ct);
                Console.WriteLine();
            }
            else
            {
                input = await ReadLineAsync(ct);
            }

            // Handle empty input
            if (string.IsNullOrEmpty(input))
            {
                if (!string.IsNullOrEmpty(prompt.Default))
                {
                    return prompt.Default;
                }

                if (prompt.Required)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("  This field is required. Please enter a value.");
                    Console.ResetColor();
                    continue;
                }

                return string.Empty;
            }

            // Handle choice selection
            if (prompt.Choices.Length > 0)
            {
                if (int.TryParse(input, out int choiceIndex) && choiceIndex >= 1 && choiceIndex <= prompt.Choices.Length)
                {
                    return prompt.Choices[choiceIndex - 1];
                }

                // Also allow typing the choice value directly
                var matchingChoice = prompt.Choices.FirstOrDefault(c =>
                    c.Equals(input, StringComparison.OrdinalIgnoreCase));

                if (matchingChoice != null)
                {
                    return matchingChoice;
                }

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  Invalid choice. Please enter a number between 1 and {prompt.Choices.Length}.");
                Console.ResetColor();
                continue;
            }

            // Validate against pattern
            if (!string.IsNullOrEmpty(prompt.Pattern))
            {
                var regex = new Regex(prompt.Pattern);
                if (!regex.IsMatch(input))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    var errorMsg = !string.IsNullOrEmpty(prompt.ValidationMessage)
                        ? prompt.ValidationMessage
                        : $"  Input does not match required pattern: {prompt.Pattern}";
                    Console.WriteLine($"  {errorMsg}");
                    Console.ResetColor();
                    continue;
                }
            }

            return input;
        }
    }

    /// <summary>
    /// Reads a line of input asynchronously.
    /// </summary>
    private static async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            // Check if stdin is available
            if (!Console.IsInputRedirected && Console.KeyAvailable == false && ct.IsCancellationRequested)
            {
                return null;
            }
            return Console.ReadLine();
        }, ct);
    }

    /// <summary>
    /// Reads a secret (password) input, masking the characters.
    /// </summary>
    private static async Task<string> ReadSecretAsync(CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var secret = new System.Text.StringBuilder();

            while (true)
            {
                if (ct.IsCancellationRequested)
                    break;

                if (!Console.KeyAvailable)
                {
                    Thread.Sleep(10);
                    continue;
                }

                var key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Enter)
                    break;

                if (key.Key == ConsoleKey.Backspace && secret.Length > 0)
                {
                    secret.Length--;
                    Console.Write("\b \b");
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    secret.Append(key.KeyChar);
                    Console.Write('*');
                }
            }

            return secret.ToString();
        }, ct);
    }
}
