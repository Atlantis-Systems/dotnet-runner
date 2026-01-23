namespace Rot.Services;

public static class TasksFileDiscovery
{
    private static readonly string[] TasksFileNames = { "tasks.yaml", "tasks.yml", "tasks.json" };

    /// <summary>
    /// Searches up the directory tree for a tasks file starting from the specified directory.
    /// </summary>
    /// <param name="startDir">The directory to start searching from. If null, uses current directory.</param>
    /// <returns>The full path to the tasks file, or null if not found.</returns>
    public static string? FindTasksFile(string? startDir = null)
    {
        var dir = startDir ?? Directory.GetCurrentDirectory();

        while (!string.IsNullOrEmpty(dir))
        {
            foreach (var fileName in TasksFileNames)
            {
                var filePath = Path.Combine(dir, fileName);
                if (File.Exists(filePath))
                {
                    return filePath;
                }
            }

            // Move up to parent directory
            var parentDir = Path.GetDirectoryName(dir);
            if (parentDir == dir)
            {
                // We've reached the root
                break;
            }
            dir = parentDir;
        }

        return null;
    }

    /// <summary>
    /// Finds a tasks file, with fallback to a specified path.
    /// </summary>
    /// <param name="specifiedPath">Explicitly specified path, if any.</param>
    /// <returns>The path to use (discovered or specified).</returns>
    public static string FindTasksFileOrDefault(string? specifiedPath)
    {
        // If a path was explicitly specified, use it
        if (!string.IsNullOrEmpty(specifiedPath))
        {
            return specifiedPath;
        }

        // Try auto-discovery
        var discovered = FindTasksFile();
        if (discovered != null)
        {
            return discovered;
        }

        // Fall back to default names in current directory
        if (File.Exists("tasks.yaml"))
        {
            return "tasks.yaml";
        }

        return "tasks.json";
    }

    /// <summary>
    /// Gets the directory containing the tasks file.
    /// </summary>
    public static string? GetTasksFileDirectory(string? specifiedPath = null)
    {
        var filePath = FindTasksFileOrDefault(specifiedPath);
        if (File.Exists(filePath))
        {
            return Path.GetDirectoryName(Path.GetFullPath(filePath));
        }
        return null;
    }
}
