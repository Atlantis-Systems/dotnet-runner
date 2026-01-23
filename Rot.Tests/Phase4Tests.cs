using Rot.Models;
using Rot.Services;

namespace Rot.Tests;

public class Phase4Tests
{
    #region Task Aliases Tests

    [Fact]
    public void HasAlias_ExistingAlias_ReturnsTrue()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build" },
            ["test"] = new TaskDefinition { Command = "echo test" }
        };
        var aliases = new Dictionary<string, string[]>
        {
            ["ci"] = new[] { "build", "test" }
        };
        var executor = new TaskExecutor(tasks, aliases);

        Assert.True(executor.HasAlias("ci"));
    }

    [Fact]
    public void HasAlias_NonExistingAlias_ReturnsFalse()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build" }
        };
        var aliases = new Dictionary<string, string[]>
        {
            ["ci"] = new[] { "build" }
        };
        var executor = new TaskExecutor(tasks, aliases);

        Assert.False(executor.HasAlias("nonexistent"));
    }

    [Fact]
    public void HasTaskOrAlias_ExistingTask_ReturnsTrue()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build" }
        };
        var executor = new TaskExecutor(tasks);

        Assert.True(executor.HasTaskOrAlias("build"));
    }

    [Fact]
    public void HasTaskOrAlias_ExistingAlias_ReturnsTrue()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build" }
        };
        var aliases = new Dictionary<string, string[]>
        {
            ["ci"] = new[] { "build" }
        };
        var executor = new TaskExecutor(tasks, aliases);

        Assert.True(executor.HasTaskOrAlias("ci"));
    }

    [Fact]
    public void HasTaskOrAlias_NeitherExists_ReturnsFalse()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build" }
        };
        var executor = new TaskExecutor(tasks);

        Assert.False(executor.HasTaskOrAlias("nonexistent"));
    }

    [Fact]
    public void GetAliasTasks_ExistingAlias_ReturnsTaskList()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build" },
            ["test"] = new TaskDefinition { Command = "echo test" }
        };
        var aliases = new Dictionary<string, string[]>
        {
            ["ci"] = new[] { "build", "test" }
        };
        var executor = new TaskExecutor(tasks, aliases);

        var aliasTasks = executor.GetAliasTasks("ci");

        Assert.Equal(2, aliasTasks.Length);
        Assert.Equal("build", aliasTasks[0]);
        Assert.Equal("test", aliasTasks[1]);
    }

    [Fact]
    public void GetAliasTasks_NonExistingAlias_ReturnsEmptyArray()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build" }
        };
        var executor = new TaskExecutor(tasks);

        var aliasTasks = executor.GetAliasTasks("nonexistent");

        Assert.Empty(aliasTasks);
    }

    [Fact]
    public void GetAliasNames_ReturnsAllAliasNames()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build" },
            ["test"] = new TaskDefinition { Command = "echo test" },
            ["clean"] = new TaskDefinition { Command = "echo clean" }
        };
        var aliases = new Dictionary<string, string[]>
        {
            ["ci"] = new[] { "build", "test" },
            ["rebuild"] = new[] { "clean", "build" }
        };
        var executor = new TaskExecutor(tasks, aliases);

        var aliasNames = executor.GetAliasNames().ToList();

        Assert.Equal(2, aliasNames.Count);
        Assert.Contains("ci", aliasNames);
        Assert.Contains("rebuild", aliasNames);
    }

    [Fact]
    public async Task ExecuteAliasAsync_ValidAlias_ExecutesAllTasks()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["task1"] = new TaskDefinition { Command = "echo task1", Type = "shell" },
            ["task2"] = new TaskDefinition { Command = "echo task2", Type = "shell" }
        };
        var aliases = new Dictionary<string, string[]>
        {
            ["both"] = new[] { "task1", "task2" }
        };
        var executor = new TaskExecutor(tasks, aliases);

        var result = await executor.ExecuteAliasAsync("both");

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ExecuteAliasAsync_NonExistingAlias_ReturnsOne()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build" }
        };
        var executor = new TaskExecutor(tasks);

        var result = await executor.ExecuteAliasAsync("nonexistent");

        Assert.Equal(1, result);
    }

    #endregion

    #region Shell Completions Tests

    [Fact]
    public void GenerateBashCompletion_ReturnsValidScript()
    {
        var completion = ShellCompletionGenerator.GenerateBashCompletion();

        Assert.NotEmpty(completion);
        Assert.Contains("_rot_completions", completion);
        Assert.Contains("complete -F _rot_completions rot", completion);
    }

    [Fact]
    public void GenerateZshCompletion_ReturnsValidScript()
    {
        var completion = ShellCompletionGenerator.GenerateZshCompletion();

        Assert.NotEmpty(completion);
        Assert.Contains("#compdef rot", completion);
        Assert.Contains("_rot()", completion);
    }

    [Fact]
    public void GenerateFishCompletion_ReturnsValidScript()
    {
        var completion = ShellCompletionGenerator.GenerateFishCompletion();

        Assert.NotEmpty(completion);
        Assert.Contains("complete -c rot", completion);
    }

    [Fact]
    public void GeneratePowerShellCompletion_ReturnsValidScript()
    {
        var completion = ShellCompletionGenerator.GeneratePowerShellCompletion();

        Assert.NotEmpty(completion);
        Assert.Contains("Register-ArgumentCompleter", completion);
        Assert.Contains("CommandName rot", completion);
    }

    [Fact]
    public void Generate_ValidShell_ReturnsCompletion()
    {
        Assert.NotEmpty(ShellCompletionGenerator.Generate("bash"));
        Assert.NotEmpty(ShellCompletionGenerator.Generate("zsh"));
        Assert.NotEmpty(ShellCompletionGenerator.Generate("fish"));
        Assert.NotEmpty(ShellCompletionGenerator.Generate("powershell"));
    }

    [Fact]
    public void Generate_InvalidShell_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => ShellCompletionGenerator.Generate("invalid"));
    }

    #endregion

    #region Config File Auto-Discovery Tests

    [Fact]
    public void FindTasksFileOrDefault_NoSpecifiedPath_UsesDefaultOrDiscovery()
    {
        var result = TasksFileDiscovery.FindTasksFileOrDefault(null);

        // Should return a default path (either discovered or fallback)
        Assert.NotEmpty(result);
    }

    [Fact]
    public void FindTasksFileOrDefault_SpecifiedPath_UsesSpecifiedPath()
    {
        var specifiedPath = "/custom/path/tasks.json";
        var result = TasksFileDiscovery.FindTasksFileOrDefault(specifiedPath);

        Assert.Equal(specifiedPath, result);
    }

    #endregion

    #region Task Graph Tests

    [Fact]
    public void PrintGraph_AllTasks_NoException()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build", DependsOn = new[] { "restore" } },
            ["test"] = new TaskDefinition { Command = "echo test", DependsOn = new[] { "build" } },
            ["restore"] = new TaskDefinition { Command = "echo restore" }
        };
        var executor = new TaskExecutor(tasks);

        var exception = Record.Exception(() => executor.PrintGraph());

        Assert.Null(exception);
    }

    [Fact]
    public void PrintGraph_SpecificTask_NoException()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build", DependsOn = new[] { "restore" } },
            ["restore"] = new TaskDefinition { Command = "echo restore" }
        };
        var executor = new TaskExecutor(tasks);

        var exception = Record.Exception(() => executor.PrintGraph("build"));

        Assert.Null(exception);
    }

    [Fact]
    public void PrintGraph_NonExistentTask_NoException()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build" }
        };
        var executor = new TaskExecutor(tasks);

        // Should handle gracefully
        var exception = Record.Exception(() => executor.PrintGraph("nonexistent"));

        Assert.Null(exception);
    }

    [Fact]
    public void PrintGraph_WithAliases_NoException()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build" },
            ["test"] = new TaskDefinition { Command = "echo test" }
        };
        var aliases = new Dictionary<string, string[]>
        {
            ["ci"] = new[] { "build", "test" }
        };
        var executor = new TaskExecutor(tasks, aliases);

        var exception = Record.Exception(() => executor.PrintGraph());

        Assert.Null(exception);
    }

    [Fact]
    public void PrintGraph_WithPrePostTasks_NoException()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition
            {
                Command = "echo build",
                PreTasks = new[] { "clean" },
                PostTasks = new[] { "notify" }
            },
            ["clean"] = new TaskDefinition { Command = "echo clean" },
            ["notify"] = new TaskDefinition { Command = "echo notify" }
        };
        var executor = new TaskExecutor(tasks);

        var exception = Record.Exception(() => executor.PrintGraph("build"));

        Assert.Null(exception);
    }

    #endregion

    #region TaskResult Tests

    [Fact]
    public void TaskResult_Succeeded_HasCorrectProperties()
    {
        var duration = TimeSpan.FromSeconds(5);
        var result = TaskResult.Succeeded("build", duration);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("build", result.TaskName);
        Assert.Equal(duration, result.Duration);
        Assert.Null(result.ErrorMessage);
        Assert.False(result.Skipped);
    }

    [Fact]
    public void TaskResult_Failed_HasCorrectProperties()
    {
        var result = TaskResult.Failed("build", 1, "Build failed");

        Assert.False(result.Success);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal("build", result.TaskName);
        Assert.Equal("Build failed", result.ErrorMessage);
    }

    [Fact]
    public void TaskResult_Skipped_HasCorrectProperties()
    {
        var result = TaskResult.SkippedResult("build", "Cached");

        Assert.True(result.Success);
        Assert.True(result.Skipped);
        Assert.Equal("Cached", result.SkipReason);
    }

    [Fact]
    public void TaskResult_TimedOut_HasCorrectProperties()
    {
        var result = TaskResult.TimedOut("build", 30);

        Assert.False(result.Success);
        Assert.Equal(-1, result.ExitCode);
        Assert.Contains("30 seconds", result.ErrorMessage);
    }

    [Fact]
    public void TaskResult_DryRun_HasCorrectProperties()
    {
        var result = TaskResult.DryRunResult("build");

        Assert.True(result.Success);
        Assert.True(result.DryRun);
    }

    [Fact]
    public void TaskResult_NotFound_HasCorrectProperties()
    {
        var result = TaskResult.NotFound("build");

        Assert.False(result.Success);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public void TaskResult_CircularDependency_HasCorrectProperties()
    {
        var result = TaskResult.CircularDependency("build");

        Assert.False(result.Success);
        Assert.Contains("Circular dependency", result.ErrorMessage);
    }

    [Fact]
    public void TasksResult_AllSucceeded_SuccessIsTrue()
    {
        var results = new TasksResult
        {
            Results = new[]
            {
                TaskResult.Succeeded("build", TimeSpan.FromSeconds(1)),
                TaskResult.Succeeded("test", TimeSpan.FromSeconds(2))
            }
        };

        Assert.True(results.Success);
        Assert.Equal(0, results.ExitCode);
        Assert.Equal(2, results.SucceededCount);
        Assert.Equal(0, results.FailedCount);
    }

    [Fact]
    public void TasksResult_OneFailed_SuccessIsFalse()
    {
        var results = new TasksResult
        {
            Results = new[]
            {
                TaskResult.Succeeded("build", TimeSpan.FromSeconds(1)),
                TaskResult.Failed("test", 1, "Test failed")
            }
        };

        Assert.False(results.Success);
        Assert.Equal(1, results.ExitCode);
        Assert.Equal(1, results.SucceededCount);
        Assert.Equal(1, results.FailedCount);
    }

    [Fact]
    public void TasksResult_TotalDuration_SumsAllDurations()
    {
        var results = new TasksResult
        {
            Results = new[]
            {
                TaskResult.Succeeded("build", TimeSpan.FromSeconds(1)),
                TaskResult.Succeeded("test", TimeSpan.FromSeconds(2)),
                TaskResult.Succeeded("deploy", TimeSpan.FromSeconds(3))
            }
        };

        Assert.Equal(TimeSpan.FromSeconds(6), results.TotalDuration);
    }

    [Fact]
    public void TasksResult_SkippedCount_CountsSkippedTasks()
    {
        var results = new TasksResult
        {
            Results = new[]
            {
                TaskResult.Succeeded("build", TimeSpan.FromSeconds(1)),
                TaskResult.SkippedResult("test", "Cached"),
                TaskResult.SkippedResult("deploy", "Condition not met")
            }
        };

        Assert.Equal(1, results.SucceededCount);
        Assert.Equal(2, results.SkippedCount);
        Assert.Equal(0, results.FailedCount);
    }

    #endregion

    #region ExecuteTaskWithResultAsync Tests

    [Fact]
    public async Task ExecuteTaskWithResultAsync_Success_ReturnsSuccessResult()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["echo"] = new TaskDefinition { Command = "echo hello", Type = "shell" }
        };
        var executor = new TaskExecutor(tasks);

        var result = await executor.ExecuteTaskWithResultAsync("echo");

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("echo", result.TaskName);
    }

    [Fact]
    public async Task ExecuteTaskWithResultAsync_NotFound_ReturnsNotFoundResult()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build" }
        };
        var executor = new TaskExecutor(tasks);

        var result = await executor.ExecuteTaskWithResultAsync("nonexistent");

        Assert.False(result.Success);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteTaskWithResultAsync_Failure_ReturnsFailedResult()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["fail"] = new TaskDefinition { Command = "exit 1", Type = "shell" }
        };
        var executor = new TaskExecutor(tasks);

        var result = await executor.ExecuteTaskWithResultAsync("fail");

        Assert.False(result.Success);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteTaskWithResultAsync_DryRun_ReturnsDryRunResult()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build", Type = "shell" }
        };
        var executor = new TaskExecutor(tasks, aliases: null, dryRun: true);

        var result = await executor.ExecuteTaskWithResultAsync("build");

        Assert.True(result.Success);
        Assert.True(result.DryRun);
    }

    [Fact]
    public async Task ExecuteTasksWithResultAsync_Success_ReturnsAggregatedResult()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["task1"] = new TaskDefinition { Command = "echo task1", Type = "shell" },
            ["task2"] = new TaskDefinition { Command = "echo task2", Type = "shell" }
        };
        var executor = new TaskExecutor(tasks);

        var result = await executor.ExecuteTasksWithResultAsync(new[] { "task1", "task2" });

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(2, result.Results.Count);
    }

    #endregion
}
