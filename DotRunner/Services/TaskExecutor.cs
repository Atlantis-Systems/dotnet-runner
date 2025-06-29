using System.Diagnostics;
using System.Text.Json;
using DotRunner.Models;

namespace DotRunner.Services;

public class TaskExecutor
{
    private readonly Dictionary<string, TaskDefinition> _tasks;
    private readonly HashSet<string> _executingTasks = new();
    private readonly SemaphoreSlim _executionSemaphore = new(1, 1);
    private readonly bool _allowConcurrency;

    public TaskExecutor(Dictionary<string, TaskDefinition> tasks, bool allowConcurrency = false)
    {
        _tasks = tasks;
        _allowConcurrency = allowConcurrency;
    }

    public static TaskExecutor LoadFromFile(string filePath, bool allowConcurrency = false)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Tasks file not found: {filePath}");

        var json = File.ReadAllText(filePath);
        var config = JsonSerializer.Deserialize<TasksConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return new TaskExecutor(config?.Tasks ?? new Dictionary<string, TaskDefinition>(), allowConcurrency);
    }

    public async Task<int> ExecuteTaskAsync(string taskName)
    {
        if (!_tasks.ContainsKey(taskName))
        {
            Console.WriteLine($"Task '{taskName}' not found.");
            return 1;
        }

        await _executionSemaphore.WaitAsync();
        try
        {
            if (_executingTasks.Contains(taskName))
            {
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
            var dependencyTasks = task.DependsOn.Select(ExecuteTaskAsync);
            var dependencyResults = await Task.WhenAll(dependencyTasks);
            
            var failedDependency = dependencyResults.FirstOrDefault(r => r != 0);
            if (failedDependency != 0)
            {
                await _executionSemaphore.WaitAsync();
                try { _executingTasks.Remove(taskName); } finally { _executionSemaphore.Release(); }
                Console.WriteLine($"One or more dependencies failed for task '{taskName}'.");
                return failedDependency;
            }
        }
        else
        {
            foreach (var dependency in task.DependsOn)
            {
                var dependencyResult = await ExecuteTaskAsync(dependency);
                if (dependencyResult != 0)
                {
                    await _executionSemaphore.WaitAsync();
                    try { _executingTasks.Remove(taskName); } finally { _executionSemaphore.Release(); }
                    Console.WriteLine($"Dependency '{dependency}' failed for task '{taskName}'.");
                    return dependencyResult;
                }
            }
        }

        try
        {
            Console.WriteLine($"Executing task: {task.Label ?? taskName}");
            
            var result = await RunCommandAsync(task);
            
            if (result == 0)
            {
                Console.WriteLine($"Task '{taskName}' completed successfully.");
            }
            else
            {
                Console.WriteLine($"Task '{taskName}' failed with exit code {result}.");
            }

            return result;
        }
        finally
        {
            await _executionSemaphore.WaitAsync();
            try { _executingTasks.Remove(taskName); } finally { _executionSemaphore.Release(); }
        }
    }

    private async Task<int> RunCommandAsync(TaskDefinition task)
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
                    processInfo.FileName = task.Options.Shell ?? "cmd.exe";
                    processInfo.Arguments = $"/c \"{task.Command}\"";
                }
                else
                {
                    processInfo.FileName = task.Options.Shell ?? "/bin/bash";
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

        if (!string.IsNullOrEmpty(task.Options.Cwd))
        {
            processInfo.WorkingDirectory = task.Options.Cwd;
        }

        foreach (var env in task.Options.Env)
        {
            processInfo.Environment[env.Key] = env.Value;
        }

        processInfo.UseShellExecute = false;
        processInfo.RedirectStandardOutput = true;
        processInfo.RedirectStandardError = true;
        processInfo.CreateNoWindow = true;

        using var process = new Process { StartInfo = processInfo };

        if (task.Presentation.Echo)
        {
            process.OutputDataReceived += (_, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };
        }

        process.Start();

        if (task.Presentation.Echo)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        await process.WaitForExitAsync();
        return process.ExitCode;
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