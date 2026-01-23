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

    [Fact]
    public void GetTasksByGroup_ReturnsMatchingTasks()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build", Group = "compile" },
            ["test"] = new TaskDefinition { Command = "echo test", Group = "quality" },
            ["lint"] = new TaskDefinition { Command = "echo lint", Group = "quality" },
            ["deploy"] = new TaskDefinition { Command = "echo deploy", Group = "release" }
        };
        var executor = new TaskExecutor(tasks);

        var qualityTasks = executor.GetTasksByGroup("quality").ToList();

        Assert.Equal(2, qualityTasks.Count);
        Assert.Contains("test", qualityTasks);
        Assert.Contains("lint", qualityTasks);
    }

    [Fact]
    public void GetTasksByGroup_CaseInsensitive_ReturnsMatchingTasks()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build", Group = "Compile" }
        };
        var executor = new TaskExecutor(tasks);

        var result = executor.GetTasksByGroup("compile").ToList();

        Assert.Single(result);
        Assert.Contains("build", result);
    }

    [Fact]
    public void GetTasksByGroup_NoMatch_ReturnsEmpty()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build", Group = "compile" }
        };
        var executor = new TaskExecutor(tasks);

        var result = executor.GetTasksByGroup("nonexistent").ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void GetTasksByPattern_Wildcard_ReturnsMatchingTasks()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["test-unit"] = new TaskDefinition { Command = "echo test-unit" },
            ["test-integration"] = new TaskDefinition { Command = "echo test-integration" },
            ["test-e2e"] = new TaskDefinition { Command = "echo test-e2e" },
            ["build"] = new TaskDefinition { Command = "echo build" }
        };
        var executor = new TaskExecutor(tasks);

        var testTasks = executor.GetTasksByPattern("test-*").ToList();

        Assert.Equal(3, testTasks.Count);
        Assert.Contains("test-unit", testTasks);
        Assert.Contains("test-integration", testTasks);
        Assert.Contains("test-e2e", testTasks);
        Assert.DoesNotContain("build", testTasks);
    }

    [Fact]
    public void GetTasksByPattern_QuestionMark_ReturnsMatchingTasks()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["task1"] = new TaskDefinition { Command = "echo task1" },
            ["task2"] = new TaskDefinition { Command = "echo task2" },
            ["task10"] = new TaskDefinition { Command = "echo task10" }
        };
        var executor = new TaskExecutor(tasks);

        var result = executor.GetTasksByPattern("task?").ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains("task1", result);
        Assert.Contains("task2", result);
        Assert.DoesNotContain("task10", result);
    }

    [Fact]
    public void GetTasksByTag_ReturnsMatchingTasks()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build", Tags = new[] { "ci", "dev" } },
            ["test"] = new TaskDefinition { Command = "echo test", Tags = new[] { "ci" } },
            ["deploy"] = new TaskDefinition { Command = "echo deploy", Tags = new[] { "prod" } }
        };
        var executor = new TaskExecutor(tasks);

        var ciTasks = executor.GetTasksByTag("ci").ToList();

        Assert.Equal(2, ciTasks.Count);
        Assert.Contains("build", ciTasks);
        Assert.Contains("test", ciTasks);
        Assert.DoesNotContain("deploy", ciTasks);
    }

    [Fact]
    public async Task ExecuteTasksAsync_MultipleTasksSucceed_ReturnsZero()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["task1"] = new TaskDefinition { Command = "echo task1", Type = "shell" },
            ["task2"] = new TaskDefinition { Command = "echo task2", Type = "shell" }
        };
        var executor = new TaskExecutor(tasks);

        var result = await executor.ExecuteTasksAsync(new[] { "task1", "task2" });

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ExecuteTasksAsync_EmptyList_ReturnsOne()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["task1"] = new TaskDefinition { Command = "echo task1", Type = "shell" }
        };
        var executor = new TaskExecutor(tasks);

        var result = await executor.ExecuteTasksAsync(Array.Empty<string>());

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithVariableSubstitution_SubstitutesVariable()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["greet"] = new TaskDefinition
            {
                Command = "echo ${greeting} ${name}",
                Type = "shell"
            }
        };
        var variables = new Dictionary<string, string>
        {
            ["greeting"] = "Hello",
            ["name"] = "World"
        };
        var executor = new TaskExecutor(tasks, variables);

        var result = await executor.ExecuteTaskAsync("greet");

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithEnvironmentVariableSubstitution_SubstitutesEnv()
    {
        // Set up environment variable
        Environment.SetEnvironmentVariable("ROT_TEST_VAR", "TestValue");

        try
        {
            var tasks = new Dictionary<string, TaskDefinition>
            {
                ["env-sub"] = new TaskDefinition
                {
                    Command = "echo ${env:ROT_TEST_VAR}",
                    Type = "shell"
                }
            };
            var executor = new TaskExecutor(tasks);

            var result = await executor.ExecuteTaskAsync("env-sub");

            Assert.Equal(0, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ROT_TEST_VAR", null);
        }
    }

    [Fact]
    public void DescribeTask_ExistingTask_NoException()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition
            {
                Label = "Build the project",
                Command = "dotnet build",
                Type = "shell",
                Group = "compile",
                Tags = new[] { "ci", "dev" },
                DependsOn = new[] { "restore" },
                Env = new Dictionary<string, string> { ["CONFIG"] = "Release" },
                Timeout = 300
            },
            ["restore"] = new TaskDefinition { Command = "dotnet restore" }
        };
        var executor = new TaskExecutor(tasks);

        // Should not throw
        var exception = Record.Exception(() => executor.DescribeTask("build"));

        Assert.Null(exception);
    }

    [Fact]
    public void DescribeTask_NonExistingTask_NoException()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build" }
        };
        var executor = new TaskExecutor(tasks);

        // Should not throw, just prints error
        var exception = Record.Exception(() => executor.DescribeTask("nonexistent"));

        Assert.Null(exception);
    }

    [Fact]
    public void ListTasks_MultipleTasks_NoException()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition
            {
                Label = "Build the project",
                Command = "dotnet build",
                Group = "compile",
                DependsOn = new[] { "restore" }
            },
            ["test"] = new TaskDefinition
            {
                Label = "Run tests",
                Command = "dotnet test",
                Group = "quality"
            },
            ["restore"] = new TaskDefinition
            {
                Label = "Restore packages",
                Command = "dotnet restore"
            }
        };
        var executor = new TaskExecutor(tasks);

        // Should not throw
        var exception = Record.Exception(() => executor.ListTasks());

        Assert.Null(exception);
    }

    [Fact]
    public void ListTasks_Detailed_NoException()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition
            {
                Label = "Build",
                Command = "dotnet build",
                Group = "compile"
            }
        };
        var executor = new TaskExecutor(tasks);

        // Should not throw
        var exception = Record.Exception(() => executor.ListTasks(detailed: true));

        Assert.Null(exception);
    }
}
