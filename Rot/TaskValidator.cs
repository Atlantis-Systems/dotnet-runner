using Rot.Models;

namespace Rot.Services;

public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();

    public void AddError(string error) => Errors.Add(error);
    public void AddWarning(string warning) => Warnings.Add(warning);
}

public class TaskValidator
{
    private static readonly HashSet<string> ValidTaskTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "shell",
        "process"
    };

    public ValidationResult Validate(string taskName, TaskDefinition task)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(taskName))
        {
            result.AddError("Task name cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(task.Command))
        {
            result.AddError($"Task '{taskName}': 'command' is required.");
        }

        if (!string.IsNullOrEmpty(task.Type) && !ValidTaskTypes.Contains(task.Type))
        {
            result.AddError($"Task '{taskName}': Invalid task type '{task.Type}'. Valid types are: {string.Join(", ", ValidTaskTypes)}.");
        }

        if (task.Timeout.HasValue && task.Timeout.Value <= 0)
        {
            result.AddError($"Task '{taskName}': Timeout must be a positive number (got {task.Timeout.Value}).");
        }

        if (!string.IsNullOrEmpty(task.Cwd) && !Directory.Exists(task.Cwd))
        {
            result.AddWarning($"Task '{taskName}': Working directory '{task.Cwd}' does not exist.");
        }

        return result;
    }

    public ValidationResult ValidateAll(Dictionary<string, TaskDefinition> tasks)
    {
        var result = new ValidationResult();

        if (tasks.Count == 0)
        {
            result.AddWarning("No tasks defined in configuration.");
            return result;
        }

        foreach (var (taskName, task) in tasks)
        {
            var taskResult = Validate(taskName, task);
            foreach (var error in taskResult.Errors)
            {
                result.AddError(error);
            }
            foreach (var warning in taskResult.Warnings)
            {
                result.AddWarning(warning);
            }
        }

        // Validate dependencies
        foreach (var (taskName, task) in tasks)
        {
            foreach (var dependency in task.DependsOn)
            {
                if (!tasks.ContainsKey(dependency))
                {
                    result.AddError($"Task '{taskName}': Dependency '{dependency}' not found.");
                }
            }
        }

        // Detect circular dependencies
        var circularDeps = DetectCircularDependencies(tasks);
        foreach (var cycle in circularDeps)
        {
            result.AddError($"Circular dependency detected: {cycle}");
        }

        return result;
    }

    private List<string> DetectCircularDependencies(Dictionary<string, TaskDefinition> tasks)
    {
        var cycles = new List<string>();
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        var path = new List<string>();

        foreach (var taskName in tasks.Keys)
        {
            if (!visited.Contains(taskName))
            {
                DetectCycleDfs(taskName, tasks, visited, recursionStack, path, cycles);
            }
        }

        return cycles;
    }

    private void DetectCycleDfs(
        string taskName,
        Dictionary<string, TaskDefinition> tasks,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        List<string> path,
        List<string> cycles)
    {
        visited.Add(taskName);
        recursionStack.Add(taskName);
        path.Add(taskName);

        if (tasks.TryGetValue(taskName, out var task))
        {
            foreach (var dependency in task.DependsOn)
            {
                if (!visited.Contains(dependency))
                {
                    DetectCycleDfs(dependency, tasks, visited, recursionStack, path, cycles);
                }
                else if (recursionStack.Contains(dependency))
                {
                    var cycleStart = path.IndexOf(dependency);
                    var cycle = string.Join(" -> ", path.Skip(cycleStart)) + " -> " + dependency;
                    cycles.Add(cycle);
                }
            }
        }

        path.RemoveAt(path.Count - 1);
        recursionStack.Remove(taskName);
    }
}
