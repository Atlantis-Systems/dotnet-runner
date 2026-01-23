using System.Diagnostics;
using System.Text.Json;
using Rot.Logging;
using Rot.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Rot.Services;

public class TaskExecutor
{
    private readonly Dictionary<string, TaskDefinition> _tasks;
    private readonly HashSet<string> _executingTasks = new();
    private readonly SemaphoreSlim _executionSemaphore = new(1, 1);
    private readonly bool _allowConcurrency;
    private readonly bool _dryRun;
    private readonly ITaskLogger _logger;

    public TaskExecutor(
        Dictionary<string, TaskDefinition> tasks,
        bool allowConcurrency = false,
        bool dryRun = false,
        ITaskLogger? logger = null)
    {
        _tasks = tasks;
        _allowConcurrency = allowConcurrency;
        _dryRun = dryRun;
        _logger = logger ?? NullLogger.Instance;
    }

    public static TaskExecutor LoadFromFile(
        string filePath,
        bool allowConcurrency = false,
        bool dryRun = false,
        ITaskLogger? logger = null)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Tasks file not found: {filePath}");

        var fileContent = File.ReadAllText(filePath);
        TasksConfig? config = null;
        
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        switch (extension)
        {
            case ".json":
                config = JsonSerializer.Deserialize<TasksConfig>(fileContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                break;
                
            case ".yaml":
            case ".yml":
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                config = deserializer.Deserialize<TasksConfig>(fileContent);
                break;
                
            default:
                throw new NotSupportedException($"File extension '{extension}' is not supported. Use .json, .yaml, or .yml files.");
        }

        var tasks = config?.Tasks ?? new Dictionary<string, TaskDefinition>();

        // Validate configuration
        var validator = new TaskValidator();
        var validationResult = validator.ValidateAll(tasks);

        foreach (var warning in validationResult.Warnings)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Warning: {warning}");
            Console.ResetColor();
        }

        if (!validationResult.IsValid)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Configuration validation failed:");
            foreach (var error in validationResult.Errors)
            {
                Console.WriteLine($"  - {error}");
            }
            Console.ResetColor();
            throw new InvalidOperationException("Task configuration is invalid.");
        }

        return new TaskExecutor(tasks, allowConcurrency, dryRun, logger);
    }

    public async Task<int> ExecuteTaskAsync(string taskName)
    {
        _logger.Debug("Starting execution of task '{TaskName}'", taskName);

        if (!_tasks.ContainsKey(taskName))
        {
            _logger.Error("Task '{TaskName}' not found", taskName);
            PrintTaskNotFoundError(taskName);
            return 1;
        }

        await _executionSemaphore.WaitAsync();
        try
        {
            if (_executingTasks.Contains(taskName))
            {
                _logger.Error("Circular dependency detected for task '{TaskName}'", taskName);
                Console.WriteLine($"Circular dependency detected for task '{taskName}'.");
                return 1;
            }
            _executingTasks.Add(taskName);
        }
        finally
        {
            _executionSemaphore.Release();
        }

        var task = _tasks[taskName];

        if (_allowConcurrency && task.AllowConcurrent && task.DependsOn.Length > 0)
        {
            _logger.Debug("Executing {Count} dependencies concurrently for task '{TaskName}'", task.DependsOn.Length, taskName);
            var dependencyTasks = task.DependsOn.Select(ExecuteTaskAsync);
            var dependencyResults = await Task.WhenAll(dependencyTasks);

            var failedDependency = dependencyResults.FirstOrDefault(r => r != 0);
            if (failedDependency != 0)
            {
                await _executionSemaphore.WaitAsync();
                try { _executingTasks.Remove(taskName); } finally { _executionSemaphore.Release(); }
                _logger.Error("One or more dependencies failed for task '{TaskName}'", taskName);
                Console.WriteLine($"One or more dependencies failed for task '{taskName}'.");
                return failedDependency;
            }
        }
        else
        {
            foreach (var dependency in task.DependsOn)
            {
                _logger.Debug("Executing dependency '{Dependency}' for task '{TaskName}'", dependency, taskName);
                var dependencyResult = await ExecuteTaskAsync(dependency);
                if (dependencyResult != 0)
                {
                    await _executionSemaphore.WaitAsync();
                    try { _executingTasks.Remove(taskName); } finally { _executionSemaphore.Release(); }
                    _logger.Error("Dependency '{Dependency}' failed for task '{TaskName}'", dependency, taskName);
                    Console.WriteLine($"Dependency '{dependency}' failed for task '{taskName}'.");
                    return dependencyResult;
                }
            }
        }

        try
        {
            var taskLabel = GetColoredTaskLabel(taskName);

            if (_dryRun)
            {
                PrintDryRunInfo(task, taskName, taskLabel);
                return 0;
            }

            _logger.Info("Executing task '{TaskName}': {Command}", taskName, task.Command);
            Console.WriteLine($"{taskLabel} Executing task...");

            var stopwatch = Stopwatch.StartNew();
            var result = await RunCommandAsync(task, taskName);
            stopwatch.Stop();

            if (result == 0)
            {
                _logger.Info("Task '{TaskName}' completed successfully in {Duration}ms", taskName, stopwatch.ElapsedMilliseconds);
                Console.WriteLine($"{taskLabel} Task completed successfully.");
            }
            else if (result == -1)
            {
                _logger.Error("Task '{TaskName}' timed out after {Timeout} seconds", taskName, task.Timeout);
                Console.WriteLine($"{taskLabel} Task timed out after {task.Timeout} seconds.");
            }
            else
            {
                _logger.Error("Task '{TaskName}' failed with exit code {ExitCode}", taskName, result);
                Console.WriteLine($"{taskLabel} Task failed with exit code {result}.");
            }

            return result;
        }
        finally
        {
            await _executionSemaphore.WaitAsync();
            try { _executingTasks.Remove(taskName); } finally { _executionSemaphore.Release(); }
        }
    }

    private string GetColoredTaskLabel(string taskName)
    {
        var task = _tasks[taskName];
        var displayLabel = !string.IsNullOrEmpty(task.Label) ? task.Label : taskName;
        
        var colors = new[] { ConsoleColor.Cyan, ConsoleColor.Green, ConsoleColor.Yellow, ConsoleColor.Magenta, ConsoleColor.Blue };
        var colorIndex = Math.Abs(taskName.GetHashCode()) % colors.Length;
        var color = colors[colorIndex];
        
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.ForegroundColor = originalColor;
        return $"\u001b[1m\u001b[{GetAnsiColorCode(color)}m({displayLabel})\u001b[0m";
    }
    
    private int GetAnsiColorCode(ConsoleColor color)
    {
        return color switch
        {
            ConsoleColor.Black => 30,
            ConsoleColor.Red => 31,
            ConsoleColor.Green => 32,
            ConsoleColor.Yellow => 33,
            ConsoleColor.Blue => 34,
            ConsoleColor.Magenta => 35,
            ConsoleColor.Cyan => 36,
            ConsoleColor.White => 37,
            _ => 37
        };
    }

    private async Task<int> RunCommandAsync(TaskDefinition task, string taskName)
    {
        var processInfo = new ProcessStartInfo();

        if (task.Type == "shell" || string.IsNullOrEmpty(task.Type))
        {
            if (task.Args.Length > 0)
            {
                processInfo.FileName = task.Command;
                processInfo.Arguments = string.Join(" ", task.Args);
            }
            else
            {
                if (OperatingSystem.IsWindows())
                {
                    processInfo.FileName = task.Shell ?? "cmd.exe";
                    processInfo.Arguments = $"/c \"{task.Command}\"";
                }
                else
                {
                    processInfo.FileName = task.Shell ?? "/bin/bash";
                    processInfo.Arguments = $"-c \"{task.Command}\"";
                }
            }
        }
        else if (task.Type == "process")
        {
            processInfo.FileName = task.Command;
            processInfo.Arguments = string.Join(" ", task.Args);
        }
        else
        {
            Console.WriteLine($"Unsupported task type: {task.Type}");
            return 1;
        }

        if (!string.IsNullOrEmpty(task.Cwd))
        {
            processInfo.WorkingDirectory = task.Cwd;
        }

        foreach (var env in task.Env)
        {
            processInfo.Environment[env.Key] = env.Value;
        }

        processInfo.UseShellExecute = false;
        processInfo.RedirectStandardOutput = true;
        processInfo.RedirectStandardError = true;
        processInfo.CreateNoWindow = true;

        using var process = new Process { StartInfo = processInfo };

        if (task.Echo)
        {
            var taskLabel = GetColoredTaskLabel(taskName);
            process.OutputDataReceived += (_, e) => { if (e.Data != null) Console.WriteLine($"{taskLabel} {e.Data}"); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) Console.Error.WriteLine($"{taskLabel} {e.Data}"); };
        }

        process.Start();

        if (task.Echo)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        if (task.Timeout.HasValue && task.Timeout.Value > 0)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(task.Timeout.Value));
            try
            {
                await process.WaitForExitAsync(cts.Token);
                return process.ExitCode;
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Process may have already exited
                }
                return -1;
            }
        }

        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    private void PrintDryRunInfo(TaskDefinition task, string taskName, string taskLabel)
    {
        Console.WriteLine($"{taskLabel} [DRY RUN] Would execute:");
        Console.WriteLine($"  Command: {task.Command}");
        if (task.Args.Length > 0)
        {
            Console.WriteLine($"  Arguments: {string.Join(" ", task.Args)}");
        }
        if (!string.IsNullOrEmpty(task.Cwd))
        {
            Console.WriteLine($"  Working directory: {task.Cwd}");
        }
        if (task.Env.Count > 0)
        {
            Console.WriteLine($"  Environment:");
            foreach (var env in task.Env)
            {
                Console.WriteLine($"    {env.Key}={env.Value}");
            }
        }
        if (task.Timeout.HasValue)
        {
            Console.WriteLine($"  Timeout: {task.Timeout.Value} seconds");
        }
        if (task.DependsOn.Length > 0)
        {
            Console.WriteLine($"  Dependencies: {string.Join(", ", task.DependsOn)}");
        }
    }

    public bool HasTask(string taskName)
    {
        return _tasks.ContainsKey(taskName);
    }

    public IEnumerable<string> GetTaskNames()
    {
        return _tasks.Keys;
    }

    private void PrintTaskNotFoundError(string taskName)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Task '{taskName}' not found.");
        Console.ResetColor();

        var availableTasks = _tasks.Keys.ToList();
        if (availableTasks.Count > 0)
        {
            Console.WriteLine($"Available tasks: {string.Join(", ", availableTasks)}");

            // Suggest similar task names
            var similar = FindSimilarTasks(taskName, availableTasks);
            if (similar.Count > 0)
            {
                Console.WriteLine($"Did you mean: {string.Join(", ", similar)}?");
            }
        }
    }

    private List<string> FindSimilarTasks(string input, List<string> taskNames)
    {
        return taskNames
            .Select(name => (name, distance: LevenshteinDistance(input.ToLower(), name.ToLower())))
            .Where(x => x.distance <= 3)
            .OrderBy(x => x.distance)
            .Take(3)
            .Select(x => x.name)
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

    public void ListTasks()
    {
        Console.WriteLine("Available tasks:");
        foreach (var kvp in _tasks)
        {
            var task = kvp.Value;
            var label = !string.IsNullOrEmpty(task.Label) ? task.Label : kvp.Key;
            Console.WriteLine($"  {kvp.Key}: {label}");
        }
    }
}