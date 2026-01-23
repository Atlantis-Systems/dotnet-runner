using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Rot.Logging;
using Rot.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Rot.Services;

public class TaskExecutor
{
    private readonly Dictionary<string, TaskDefinition> _tasks;
    private readonly Dictionary<string, string> _variables;
    private readonly Dictionary<string, string> _profileEnv;
    private readonly HashSet<string> _executingTasks = new();
    private readonly HashSet<string> _completedTasks = new();
    private readonly SemaphoreSlim _executionSemaphore = new(1, 1);
    private readonly bool _allowConcurrency;
    private readonly bool _dryRun;
    private readonly bool _noCache;
    private readonly ITaskLogger _logger;
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly CacheManager _cacheManager;
    private readonly PluginLoader _pluginLoader;

    public TaskExecutor(
        Dictionary<string, TaskDefinition> tasks,
        Dictionary<string, string>? variables = null,
        Dictionary<string, string>? profileEnv = null,
        bool allowConcurrency = false,
        bool dryRun = false,
        bool noCache = false,
        ITaskLogger? logger = null,
        PluginLoader? pluginLoader = null)
    {
        _tasks = tasks;
        _variables = variables ?? new Dictionary<string, string>();
        _profileEnv = profileEnv ?? new Dictionary<string, string>();
        _allowConcurrency = allowConcurrency;
        _dryRun = dryRun;
        _noCache = noCache;
        _logger = logger ?? NullLogger.Instance;
        _conditionEvaluator = new ConditionEvaluator(_logger);
        _cacheManager = new CacheManager(_logger);
        _pluginLoader = pluginLoader ?? new PluginLoader(_logger);
    }

    public static TaskExecutor LoadFromFile(
        string filePath,
        bool allowConcurrency = false,
        bool dryRun = false,
        bool noCache = false,
        string? profile = null,
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
        var variables = new Dictionary<string, string>(config?.Variables ?? new Dictionary<string, string>());
        var profileEnv = new Dictionary<string, string>();

        // Apply profile if specified
        if (!string.IsNullOrEmpty(profile) && config?.Profiles != null)
        {
            if (config.Profiles.TryGetValue(profile, out var selectedProfile))
            {
                // Merge profile variables (profile overrides base)
                foreach (var (key, value) in selectedProfile.Variables)
                {
                    variables[key] = value;
                }

                // Store profile env for later application
                foreach (var (key, value) in selectedProfile.Env)
                {
                    profileEnv[key] = value;
                }

                logger?.Info("Applied profile '{Profile}'", profile);
            }
            else
            {
                var availableProfiles = string.Join(", ", config.Profiles.Keys);
                throw new InvalidOperationException($"Profile '{profile}' not found. Available profiles: {availableProfiles}");
            }
        }

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

        // Create plugin loader and load plugins if configured
        var pluginLoader = new PluginLoader(logger ?? NullLogger.Instance);
        // Note: Plugin names would come from config if we add plugins array to TasksConfig

        return new TaskExecutor(tasks, variables, profileEnv, allowConcurrency, dryRun, noCache, logger, pluginLoader);
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

        // Check if already completed in this session (for caching within a run)
        await _executionSemaphore.WaitAsync();
        try
        {
            if (_completedTasks.Contains(taskName))
            {
                _logger.Debug("Task '{TaskName}' already completed in this session", taskName);
                return 0;
            }

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

        // Check conditions before executing
        if (!_conditionEvaluator.Evaluate(task.Condition, taskName))
        {
            var taskLabel = GetColoredTaskLabel(taskName);
            Console.WriteLine($"{taskLabel} Skipped (condition not met)");
            await MarkTaskComplete(taskName);
            return 0;
        }

        // Execute dependencies
        if (_allowConcurrency && task.AllowConcurrent && task.DependsOn.Length > 0)
        {
            _logger.Debug("Executing {Count} dependencies concurrently for task '{TaskName}'", task.DependsOn.Length, taskName);
            var dependencyTasks = task.DependsOn.Select(ExecuteTaskAsync);
            var dependencyResults = await Task.WhenAll(dependencyTasks);

            var failedDependency = dependencyResults.FirstOrDefault(r => r != 0);
            if (failedDependency != 0)
            {
                await RemoveFromExecuting(taskName);
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
                    await RemoveFromExecuting(taskName);
                    _logger.Error("Dependency '{Dependency}' failed for task '{TaskName}'", dependency, taskName);
                    Console.WriteLine($"Dependency '{dependency}' failed for task '{taskName}'.");
                    return dependencyResult;
                }
            }
        }

        try
        {
            var taskLabel = GetColoredTaskLabel(taskName);

            // Execute pre-tasks (hooks)
            if (task.PreTasks.Length > 0)
            {
                _logger.Debug("Executing {Count} pre-tasks for '{TaskName}'", task.PreTasks.Length, taskName);
                foreach (var preTask in task.PreTasks)
                {
                    var preResult = await ExecuteTaskAsync(preTask);
                    if (preResult != 0)
                    {
                        _logger.Error("Pre-task '{PreTask}' failed for task '{TaskName}'", preTask, taskName);
                        Console.WriteLine($"{taskLabel} Pre-task '{preTask}' failed.");
                        return preResult;
                    }
                }
            }

            if (_dryRun)
            {
                PrintDryRunInfo(task, taskName, taskLabel);
                return 0;
            }

            // Check cache (unless disabled)
            if (!_noCache && task.Cache != null && _cacheManager.IsCacheValid(taskName, task.Cache))
            {
                Console.WriteLine($"{taskLabel} Skipped (cached)");
                await MarkTaskComplete(taskName);

                // Still run post-tasks even when cached
                if (task.PostTasks.Length > 0)
                {
                    return await ExecutePostTasks(task, taskName, taskLabel);
                }
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

                // Save cache on success
                if (task.Cache != null)
                {
                    _cacheManager.SaveCache(taskName, task.Cache);
                }

                // Execute post-tasks (hooks) on success
                if (task.PostTasks.Length > 0)
                {
                    var postResult = await ExecutePostTasks(task, taskName, taskLabel);
                    if (postResult != 0)
                    {
                        return postResult;
                    }
                }

                await MarkTaskComplete(taskName);
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
            await RemoveFromExecuting(taskName);
        }
    }

    private async Task<int> ExecutePostTasks(TaskDefinition task, string taskName, string taskLabel)
    {
        _logger.Debug("Executing {Count} post-tasks for '{TaskName}'", task.PostTasks.Length, taskName);
        foreach (var postTask in task.PostTasks)
        {
            var postResult = await ExecuteTaskAsync(postTask);
            if (postResult != 0)
            {
                _logger.Error("Post-task '{PostTask}' failed for task '{TaskName}'", postTask, taskName);
                Console.WriteLine($"{taskLabel} Post-task '{postTask}' failed.");
                return postResult;
            }
        }
        return 0;
    }

    private async Task MarkTaskComplete(string taskName)
    {
        await _executionSemaphore.WaitAsync();
        try
        {
            _completedTasks.Add(taskName);
            _executingTasks.Remove(taskName);
        }
        finally
        {
            _executionSemaphore.Release();
        }
    }

    private async Task RemoveFromExecuting(string taskName)
    {
        await _executionSemaphore.WaitAsync();
        try
        {
            _executingTasks.Remove(taskName);
        }
        finally
        {
            _executionSemaphore.Release();
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
        // Check if this is a plugin-provided task type
        var provider = _pluginLoader.GetProvider(task.Type);
        if (provider != null)
        {
            _logger.Debug("Using plugin provider for task type '{TaskType}'", task.Type);
            return await provider.ExecuteAsync(task, taskName, _logger);
        }

        var processInfo = new ProcessStartInfo();

        // Apply variable substitution to command and args
        var command = SubstituteVariables(task.Command);
        var args = task.Args.Select(SubstituteVariables).ToArray();

        if (task.Type == "shell" || string.IsNullOrEmpty(task.Type))
        {
            if (args.Length > 0)
            {
                processInfo.FileName = command;
                processInfo.Arguments = string.Join(" ", args);
            }
            else
            {
                if (OperatingSystem.IsWindows())
                {
                    processInfo.FileName = task.Shell ?? "cmd.exe";
                    processInfo.Arguments = $"/c \"{command}\"";
                }
                else
                {
                    processInfo.FileName = task.Shell ?? "/bin/bash";
                    processInfo.Arguments = $"-c \"{command}\"";
                }
            }
        }
        else if (task.Type == "process")
        {
            processInfo.FileName = command;
            processInfo.Arguments = string.Join(" ", args);
        }
        else
        {
            Console.WriteLine($"Unsupported task type: {task.Type}. Available types: {string.Join(", ", _pluginLoader.GetRegisteredTaskTypes())}");
            return 1;
        }

        if (!string.IsNullOrEmpty(task.Cwd))
        {
            processInfo.WorkingDirectory = SubstituteVariables(task.Cwd);
        }

        // Apply profile environment variables first
        foreach (var env in _profileEnv)
        {
            processInfo.Environment[env.Key] = SubstituteVariables(env.Value);
        }

        // Apply task-specific environment variables (override profile)
        foreach (var env in task.Env)
        {
            processInfo.Environment[env.Key] = SubstituteVariables(env.Value);
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
        if (_profileEnv.Count > 0 || task.Env.Count > 0)
        {
            Console.WriteLine($"  Environment:");
            foreach (var env in _profileEnv)
            {
                Console.WriteLine($"    {env.Key}={env.Value} (profile)");
            }
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
        if (task.PreTasks.Length > 0)
        {
            Console.WriteLine($"  Pre-tasks: {string.Join(", ", task.PreTasks)}");
        }
        if (task.PostTasks.Length > 0)
        {
            Console.WriteLine($"  Post-tasks: {string.Join(", ", task.PostTasks)}");
        }
        if (task.Condition != null)
        {
            Console.WriteLine($"  Condition: (configured)");
        }
        if (task.Cache != null)
        {
            Console.WriteLine($"  Cache: inputs={string.Join(", ", task.Cache.Inputs)}");
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

    public void ListTasks(bool detailed = false)
    {
        Console.WriteLine("\nAvailable tasks:");
        Console.WriteLine(new string('-', 60));

        foreach (var kvp in _tasks.OrderBy(t => t.Value.Group).ThenBy(t => t.Key))
        {
            var task = kvp.Value;
            var label = !string.IsNullOrEmpty(task.Label) ? task.Label : kvp.Key;

            // Format: taskName  description  [type]  depends: ...
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"  {kvp.Key,-20}");
            Console.ResetColor();

            Console.Write($"{label,-30}");

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{task.Type}]");
            Console.ResetColor();

            if (task.DependsOn.Length > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"  depends: {string.Join(", ", task.DependsOn)}");
                Console.ResetColor();
            }

            Console.WriteLine();

            if (detailed && !string.IsNullOrEmpty(task.Group))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"    Group: {task.Group}");
                Console.ResetColor();
            }
        }

        // Show group summary
        var groups = _tasks
            .Where(t => !string.IsNullOrEmpty(t.Value.Group))
            .GroupBy(t => t.Value.Group)
            .ToList();

        if (groups.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Groups:");
            foreach (var group in groups.OrderBy(g => g.Key))
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.Write($"  {group.Key}");
                Console.ResetColor();
                Console.WriteLine($" ({group.Count()})");
            }
        }

        Console.WriteLine();
    }

    public void DescribeTask(string taskName)
    {
        if (!_tasks.TryGetValue(taskName, out var task))
        {
            PrintTaskNotFoundError(taskName);
            return;
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Task: {taskName}");
        Console.ResetColor();
        Console.WriteLine(new string('-', 40));

        if (!string.IsNullOrEmpty(task.Label))
            Console.WriteLine($"  Label:    {task.Label}");

        Console.WriteLine($"  Type:     {task.Type}");
        Console.WriteLine($"  Command:  {task.Command}");

        if (task.Args.Length > 0)
            Console.WriteLine($"  Args:     {string.Join(" ", task.Args)}");

        if (!string.IsNullOrEmpty(task.Cwd))
            Console.WriteLine($"  Cwd:      {task.Cwd}");

        if (!string.IsNullOrEmpty(task.Group))
            Console.WriteLine($"  Group:    {task.Group}");

        if (task.Tags.Length > 0)
            Console.WriteLine($"  Tags:     {string.Join(", ", task.Tags)}");

        if (task.Timeout.HasValue)
            Console.WriteLine($"  Timeout:  {task.Timeout.Value} seconds");

        Console.WriteLine($"  Echo:     {task.Echo}");

        if (task.Env.Count > 0)
        {
            Console.WriteLine("  Env:");
            foreach (var env in task.Env)
            {
                Console.WriteLine($"    - {env.Key}={env.Value}");
            }
        }

        if (task.DependsOn.Length > 0)
        {
            Console.WriteLine($"  Dependencies: {string.Join(", ", task.DependsOn)}");
        }

        // Phase 3: Show pre/post tasks
        if (task.PreTasks.Length > 0)
        {
            Console.WriteLine($"  Pre-tasks:    {string.Join(", ", task.PreTasks)}");
        }

        if (task.PostTasks.Length > 0)
        {
            Console.WriteLine($"  Post-tasks:   {string.Join(", ", task.PostTasks)}");
        }

        // Phase 3: Show condition
        if (task.Condition != null)
        {
            Console.WriteLine("  Condition:");
            if (!string.IsNullOrEmpty(task.Condition.Os))
                Console.WriteLine($"    - OS: {task.Condition.Os}");
            if (task.Condition.Env.Count > 0)
            {
                foreach (var env in task.Condition.Env)
                {
                    Console.WriteLine($"    - Env: {env.Key}={env.Value}");
                }
            }
            if (task.Condition.FileExists.Length > 0)
                Console.WriteLine($"    - FileExists: {string.Join(", ", task.Condition.FileExists)}");
            if (task.Condition.FileNotExists.Length > 0)
                Console.WriteLine($"    - FileNotExists: {string.Join(", ", task.Condition.FileNotExists)}");
        }

        // Phase 3: Show cache config
        if (task.Cache != null)
        {
            Console.WriteLine("  Cache:");
            if (task.Cache.Inputs.Length > 0)
                Console.WriteLine($"    - Inputs: {string.Join(", ", task.Cache.Inputs)}");
            if (task.Cache.Outputs.Length > 0)
                Console.WriteLine($"    - Outputs: {string.Join(", ", task.Cache.Outputs)}");
            if (task.Cache.TtlMinutes.HasValue)
                Console.WriteLine($"    - TTL: {task.Cache.TtlMinutes.Value} minutes");
        }

        // Find tasks that depend on this task
        var dependents = _tasks
            .Where(t => t.Value.DependsOn.Contains(taskName))
            .Select(t => t.Key)
            .ToList();

        if (dependents.Count > 0)
        {
            Console.WriteLine($"  Dependents:   {string.Join(", ", dependents)}");
        }

        Console.WriteLine();
    }

    public IEnumerable<string> GetTasksByGroup(string group)
    {
        return _tasks
            .Where(t => t.Value.Group.Equals(group, StringComparison.OrdinalIgnoreCase))
            .Select(t => t.Key);
    }

    public IEnumerable<string> GetTasksByPattern(string pattern)
    {
        // Convert glob pattern to regex
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
        return _tasks.Keys.Where(name => regex.IsMatch(name));
    }

    public IEnumerable<string> GetTasksByTag(string tag)
    {
        return _tasks
            .Where(t => t.Value.Tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)))
            .Select(t => t.Key);
    }

    public async Task<int> ExecuteTasksAsync(IEnumerable<string> taskNames)
    {
        var names = taskNames.ToList();
        if (names.Count == 0)
        {
            Console.WriteLine("No tasks matched the criteria.");
            return 1;
        }

        Console.WriteLine($"Running {names.Count} task(s): {string.Join(", ", names)}");
        Console.WriteLine();

        foreach (var taskName in names)
        {
            var result = await ExecuteTaskAsync(taskName);
            if (result != 0)
            {
                return result;
            }
        }

        return 0;
    }

    private string SubstituteVariables(string input)
    {
        if (string.IsNullOrEmpty(input) || _variables.Count == 0)
            return input;

        // Pattern matches ${varName} or $varName
        var result = Regex.Replace(input, @"\$\{([^}]+)\}|\$([a-zA-Z_][a-zA-Z0-9_]*)", match =>
        {
            var varName = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;

            // Check for env: prefix to read from environment
            if (varName.StartsWith("env:"))
            {
                var envName = varName.Substring(4);
                return Environment.GetEnvironmentVariable(envName) ?? match.Value;
            }

            // Check configured variables
            if (_variables.TryGetValue(varName, out var value))
            {
                return value;
            }

            // Check environment variables as fallback
            var envValue = Environment.GetEnvironmentVariable(varName);
            if (envValue != null)
            {
                return envValue;
            }

            // Return original if not found
            _logger.Warning("Variable '{VarName}' not found", varName);
            return match.Value;
        });

        return result;
    }
}