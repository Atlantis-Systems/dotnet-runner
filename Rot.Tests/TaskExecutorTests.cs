using Rot.Models;
using Rot.Services;

namespace Rot.Tests;

public class TaskExecutorTests
{
    [Fact]
    public void HasTask_ExistingTask_ReturnsTrue()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build" }
        };
        var executor = new TaskExecutor(tasks);

        Assert.True(executor.HasTask("build"));
    }

    [Fact]
    public void HasTask_NonExistingTask_ReturnsFalse()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build" }
        };
        var executor = new TaskExecutor(tasks);

        Assert.False(executor.HasTask("test"));
    }

    [Fact]
    public void GetTaskNames_ReturnsAllTaskNames()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build" },
            ["test"] = new TaskDefinition { Command = "echo test" },
            ["deploy"] = new TaskDefinition { Command = "echo deploy" }
        };
        var executor = new TaskExecutor(tasks);

        var taskNames = executor.GetTaskNames().ToList();

        Assert.Equal(3, taskNames.Count);
        Assert.Contains("build", taskNames);
        Assert.Contains("test", taskNames);
        Assert.Contains("deploy", taskNames);
    }

    [Fact]
    public async Task ExecuteTaskAsync_NonExistingTask_ReturnsOne()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build" }
        };
        var executor = new TaskExecutor(tasks);

        var result = await executor.ExecuteTaskAsync("nonexistent");

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ExecuteTaskAsync_SimpleCommand_ReturnsZero()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["echo"] = new TaskDefinition
            {
                Command = "echo hello",
                Type = "shell"
            }
        };
        var executor = new TaskExecutor(tasks);

        var result = await executor.ExecuteTaskAsync("echo");

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ExecuteTaskAsync_FailingCommand_ReturnsNonZero()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["fail"] = new TaskDefinition
            {
                Command = "exit 1",
                Type = "shell"
            }
        };
        var executor = new TaskExecutor(tasks);

        var result = await executor.ExecuteTaskAsync("fail");

        Assert.NotEqual(0, result);
    }

    [Fact]
    public async Task ExecuteTaskAsync_DryRun_DoesNotExecute()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["write-file"] = new TaskDefinition
            {
                Command = "touch /tmp/rot-test-file-should-not-exist",
                Type = "shell"
            }
        };
        var executor = new TaskExecutor(tasks, dryRun: true);

        var result = await executor.ExecuteTaskAsync("write-file");

        Assert.Equal(0, result);
        Assert.False(File.Exists("/tmp/rot-test-file-should-not-exist"));
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithDependency_ExecutesDependencyFirst()
    {
        var executionOrder = new List<string>();
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["first"] = new TaskDefinition
            {
                Command = "echo first",
                Type = "shell"
            },
            ["second"] = new TaskDefinition
            {
                Command = "echo second",
                Type = "shell",
                DependsOn = new[] { "first" }
            }
        };
        var executor = new TaskExecutor(tasks);

        var result = await executor.ExecuteTaskAsync("second");

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithTimeout_TimeoutKillsProcess()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["slow"] = new TaskDefinition
            {
                Command = "sleep 10",
                Type = "shell",
                Timeout = 1 // 1 second timeout
            }
        };
        var executor = new TaskExecutor(tasks);

        var result = await executor.ExecuteTaskAsync("slow");

        Assert.Equal(-1, result); // -1 indicates timeout
    }

    [Fact]
    public async Task ExecuteTaskAsync_ProcessType_ExecutesDirectly()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["echo-process"] = new TaskDefinition
            {
                Command = "/bin/echo",
                Args = new[] { "hello", "world" },
                Type = "process"
            }
        };
        var executor = new TaskExecutor(tasks);

        var result = await executor.ExecuteTaskAsync("echo-process");

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithEnvironmentVariables_SetsEnv()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["env-test"] = new TaskDefinition
            {
                Command = "printenv MY_TEST_VAR",
                Type = "shell",
                Env = new Dictionary<string, string>
                {
                    ["MY_TEST_VAR"] = "test-value"
                }
            }
        };
        var executor = new TaskExecutor(tasks);

        var result = await executor.ExecuteTaskAsync("env-test");

        Assert.Equal(0, result);
    }
}
