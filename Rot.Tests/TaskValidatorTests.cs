using Rot.Models;
using Rot.Services;

namespace Rot.Tests;

public class TaskValidatorTests
{
    private readonly TaskValidator _validator = new();

    [Fact]
    public void Validate_ValidTask_ReturnsNoErrors()
    {
        var task = new TaskDefinition
        {
            Command = "echo hello",
            Type = "shell"
        };

        var result = _validator.Validate("test", task);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_EmptyCommand_ReturnsError()
    {
        var task = new TaskDefinition
        {
            Command = "",
            Type = "shell"
        };

        var result = _validator.Validate("test", task);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("'command' is required"));
    }

    [Fact]
    public void Validate_InvalidTaskType_ReturnsError()
    {
        var task = new TaskDefinition
        {
            Command = "echo hello",
            Type = "invalid"
        };

        var result = _validator.Validate("test", task);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Invalid task type"));
    }

    [Fact]
    public void Validate_NegativeTimeout_ReturnsError()
    {
        var task = new TaskDefinition
        {
            Command = "echo hello",
            Timeout = -5
        };

        var result = _validator.Validate("test", task);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Timeout must be a positive number"));
    }

    [Fact]
    public void Validate_ZeroTimeout_ReturnsError()
    {
        var task = new TaskDefinition
        {
            Command = "echo hello",
            Timeout = 0
        };

        var result = _validator.Validate("test", task);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Timeout must be a positive number"));
    }

    [Fact]
    public void Validate_PositiveTimeout_ReturnsNoErrors()
    {
        var task = new TaskDefinition
        {
            Command = "echo hello",
            Timeout = 60
        };

        var result = _validator.Validate("test", task);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateAll_MissingDependency_ReturnsError()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition
            {
                Command = "echo build",
                DependsOn = new[] { "missing-task" }
            }
        };

        var result = _validator.ValidateAll(tasks);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Dependency 'missing-task' not found"));
    }

    [Fact]
    public void ValidateAll_CircularDependency_ReturnsError()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["task-a"] = new TaskDefinition
            {
                Command = "echo a",
                DependsOn = new[] { "task-b" }
            },
            ["task-b"] = new TaskDefinition
            {
                Command = "echo b",
                DependsOn = new[] { "task-a" }
            }
        };

        var result = _validator.ValidateAll(tasks);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Circular dependency"));
    }

    [Fact]
    public void ValidateAll_EmptyTasks_ReturnsWarning()
    {
        var tasks = new Dictionary<string, TaskDefinition>();

        var result = _validator.ValidateAll(tasks);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("No tasks defined"));
    }

    [Fact]
    public void ValidateAll_ValidTaskGraph_ReturnsNoErrors()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["clean"] = new TaskDefinition { Command = "echo clean" },
            ["build"] = new TaskDefinition
            {
                Command = "echo build",
                DependsOn = new[] { "clean" }
            },
            ["test"] = new TaskDefinition
            {
                Command = "echo test",
                DependsOn = new[] { "build" }
            }
        };

        var result = _validator.ValidateAll(tasks);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
