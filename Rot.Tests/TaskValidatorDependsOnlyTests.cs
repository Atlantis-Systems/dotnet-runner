using Rot.Models;
using Rot.Services;
using Xunit;

namespace Rot.Tests;

public class TaskValidatorDependsOnlyTests
{
    private readonly TaskValidator _validator = new();

    [Fact]
    public void Validate_TaskWithDependsOnButNoCommand_IsValid()
    {
        var task = new TaskDefinition
        {
            Command = "",
            DependsOn = new[] { "build" }
        };

        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build" },
            ["dev"] = task
        };

        var result = _validator.ValidateAll(tasks);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_TaskWithPreTasksButNoCommand_IsValid()
    {
        var task = new TaskDefinition
        {
            Command = "",
            PreTasks = new[] { "setup" }
        };

        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["setup"] = new TaskDefinition { Command = "echo setup" },
            ["dev"] = task
        };

        var result = _validator.ValidateAll(tasks);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_TaskWithPostTasksButNoCommand_IsValid()
    {
        var task = new TaskDefinition
        {
            Command = "",
            PostTasks = new[] { "teardown" }
        };

        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["teardown"] = new TaskDefinition { Command = "echo teardown" },
            ["dev"] = task
        };

        var result = _validator.ValidateAll(tasks);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
