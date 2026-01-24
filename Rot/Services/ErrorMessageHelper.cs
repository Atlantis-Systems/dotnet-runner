namespace Rot.Services;

/// <summary>
/// Provides enhanced error messages with actionable context.
/// </summary>
public static class ErrorMessageHelper
{
    /// <summary>
    /// Generates an enhanced error message for a file not found error.
    /// </summary>
    public static string GetFileNotFoundMessage(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath) ?? ".";
        var fileName = Path.GetFileName(filePath);

        var message = $"Tasks file not found: {filePath}";

        // Check if directory exists
        if (!Directory.Exists(directory))
        {
            message += $"\n\nThe directory '{directory}' does not exist.";
            message += "\nCreate the directory or check the path for typos.";
        }
        else
        {
            // Look for similar files
            var similarFiles = FindSimilarFiles(directory, fileName);
            if (similarFiles.Count > 0)
            {
                message += $"\n\nDid you mean one of these files?";
                foreach (var file in similarFiles)
                {
                    message += $"\n  - {file}";
                }
            }
            else
            {
                message += $"\n\nTo create a new tasks file, run:";
                message += $"\n  rot init                     # Creates tasks.json";
                message += $"\n  rot init --format yaml       # Creates tasks.yaml";
                message += $"\n  rot init --template dotnet   # .NET project template";
                message += $"\n  rot init --template node     # Node.js project template";
            }
        }

        return message;
    }

    /// <summary>
    /// Generates an enhanced error message for a task not found error.
    /// </summary>
    public static string GetTaskNotFoundMessage(string taskName, IEnumerable<string> availableTasks, IEnumerable<string> availableAliases)
    {
        var taskList = availableTasks.ToList();
        var aliasList = availableAliases.ToList();

        var message = $"Task '{taskName}' not found.";

        // Find similar task names
        var similarTasks = FindSimilarStrings(taskName, taskList, 3);
        if (similarTasks.Count > 0)
        {
            message += $"\n\nDid you mean?";
            foreach (var task in similarTasks)
            {
                message += $"\n  - {task}";
            }
        }

        // Check if it might be an alias
        var similarAliases = FindSimilarStrings(taskName, aliasList, 2);
        if (similarAliases.Count > 0)
        {
            message += $"\n\nSimilar aliases:";
            foreach (var alias in similarAliases)
            {
                message += $"\n  - {alias}";
            }
        }

        // Show available tasks if not too many
        if (taskList.Count <= 10 && taskList.Count > 0)
        {
            message += $"\n\nAvailable tasks: {string.Join(", ", taskList.OrderBy(t => t))}";
        }
        else if (taskList.Count > 10)
        {
            message += $"\n\nRun 'rot list' to see all {taskList.Count} available tasks.";
        }

        return message;
    }

    /// <summary>
    /// Generates an enhanced error message for a circular dependency.
    /// </summary>
    public static string GetCircularDependencyMessage(string taskName, IEnumerable<string>? dependencyChain = null)
    {
        var message = $"Circular dependency detected involving task '{taskName}'.";

        if (dependencyChain != null)
        {
            var chain = dependencyChain.ToList();
            if (chain.Count > 0)
            {
                message += $"\n\nDependency chain: {string.Join(" → ", chain)} → {taskName}";
            }
        }

        message += "\n\nTo fix this:";
        message += "\n  1. Review the 'dependsOn' configuration for the involved tasks";
        message += "\n  2. Remove the circular reference";
        message += "\n  3. Consider restructuring tasks to avoid the cycle";

        return message;
    }

    /// <summary>
    /// Generates an enhanced error message for a validation error.
    /// </summary>
    public static string GetValidationErrorMessage(string taskName, IEnumerable<string> errors)
    {
        var errorList = errors.ToList();
        var message = $"Validation failed for task '{taskName}':";

        foreach (var error in errorList)
        {
            message += $"\n  - {error}";
        }

        message += "\n\nPlease fix these issues and try again.";
        message += "\nFor more information, run 'rot describe " + taskName + "'";

        return message;
    }

    /// <summary>
    /// Generates an enhanced error message for a profile not found error.
    /// </summary>
    public static string GetProfileNotFoundMessage(string profileName, IEnumerable<string> availableProfiles)
    {
        var profiles = availableProfiles.ToList();
        var message = $"Profile '{profileName}' not found.";

        if (profiles.Count > 0)
        {
            var similar = FindSimilarStrings(profileName, profiles, 2);
            if (similar.Count > 0)
            {
                message += "\n\nDid you mean?";
                foreach (var p in similar)
                {
                    message += $"\n  - {p}";
                }
            }

            message += $"\n\nAvailable profiles: {string.Join(", ", profiles.OrderBy(p => p))}";
        }
        else
        {
            message += "\n\nNo profiles are defined in your tasks file.";
            message += "\nTo add profiles, include a 'profiles' section in your tasks.json/tasks.yaml:";
            message += "\n";
            message += "\n  \"profiles\": {";
            message += "\n    \"dev\": { \"env\": { \"DEBUG\": \"true\" } },";
            message += "\n    \"prod\": { \"env\": { \"OPTIMIZE\": \"true\" } }";
            message += "\n  }";
        }

        return message;
    }

    /// <summary>
    /// Generates an enhanced error message for a dependency failure.
    /// </summary>
    public static string GetDependencyFailedMessage(string taskName, string dependencyName, int exitCode)
    {
        var message = $"Cannot run task '{taskName}' because dependency '{dependencyName}' failed.";
        message += $"\n\nDependency exit code: {exitCode}";

        message += "\n\nTo fix this:";
        message += $"\n  1. Run 'rot run {dependencyName}' to see the full error output";
        message += $"\n  2. Fix the issues in '{dependencyName}'";
        message += $"\n  3. Try running '{taskName}' again";

        return message;
    }

    /// <summary>
    /// Generates an enhanced error message for a timeout.
    /// </summary>
    public static string GetTimeoutMessage(string taskName, int timeoutSeconds)
    {
        var message = $"Task '{taskName}' timed out after {timeoutSeconds} seconds.";

        message += "\n\nPossible causes:";
        message += "\n  - The task is taking longer than expected";
        message += "\n  - The task is waiting for input or resources";
        message += "\n  - The timeout value is too short";

        message += "\n\nTo fix this:";
        message += $"\n  1. Increase the timeout value in the task configuration";
        message += $"\n  2. Run the command manually to diagnose the issue";
        message += $"\n  3. Add '--verbose' flag for more details";

        return message;
    }

    /// <summary>
    /// Generates an enhanced error message for an unsupported file format.
    /// </summary>
    public static string GetUnsupportedFormatMessage(string extension)
    {
        var message = $"File extension '{extension}' is not supported.";
        message += "\n\nSupported formats:";
        message += "\n  - .json (JSON format)";
        message += "\n  - .yaml or .yml (YAML format)";

        message += "\n\nTo convert between formats:";
        message += "\n  - Use online YAML/JSON converters";
        message += "\n  - Rename your file with the correct extension";

        return message;
    }

    private static List<string> FindSimilarFiles(string directory, string fileName)
    {
        try
        {
            var files = Directory.GetFiles(directory)
                .Select(Path.GetFileName)
                .Where(f => f != null)
                .Select(f => f!)
                .ToList();

            // Look for common task file names
            var taskFilePatterns = new[] { "tasks.json", "tasks.yaml", "tasks.yml", "Taskfile", "taskfile.json", "taskfile.yaml" };
            var matches = files.Where(f => taskFilePatterns.Any(p => f.Equals(p, StringComparison.OrdinalIgnoreCase))).ToList();

            if (matches.Count == 0)
            {
                // Find similar file names
                matches = FindSimilarStrings(fileName, files, 3);
            }

            return matches;
        }
        catch
        {
            return new List<string>();
        }
    }

    private static List<string> FindSimilarStrings(string input, IEnumerable<string> candidates, int maxResults)
    {
        return candidates
            .Select(c => (candidate: c, distance: LevenshteinDistance(input.ToLowerInvariant(), c.ToLowerInvariant())))
            .Where(x => x.distance <= Math.Max(3, input.Length / 2))
            .OrderBy(x => x.distance)
            .Take(maxResults)
            .Select(x => x.candidate)
            .ToList();
    }

    private static int LevenshteinDistance(string s1, string s2)
    {
        var m = s1.Length;
        var n = s2.Length;
        var dp = new int[m + 1, n + 1];

        for (int i = 0; i <= m; i++) dp[i, 0] = i;
        for (int j = 0; j <= n; j++) dp[0, j] = j;

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[m, n];
    }
}
