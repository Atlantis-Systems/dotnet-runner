using Rot.Models;
using Rot.Services;
using Xunit;

namespace Rot.Tests;

public class Phase5Tests
{
    #region Task Output Capture Tests

    [Fact]
    public async Task ExecuteTaskWithResultAsync_CapturesOutput_WhenEnabled()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["echo-test"] = new TaskDefinition
            {
                Command = "echo 'Hello, Output Capture!'",
                Type = "shell"
            }
        };
        var executor = new TaskExecutor(tasks, captureOutput: true);

        var result = await executor.ExecuteTaskWithResultAsync("echo-test");

        Assert.True(result.Success);
        Assert.NotNull(result.StandardOutput);
        Assert.Contains("Hello, Output Capture!", result.StandardOutput);
    }

    [Fact]
    public async Task ExecuteTaskWithResultAsync_ReturnsNullOutput_WhenDisabled()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["echo-test"] = new TaskDefinition
            {
                Command = "echo 'Hello'",
                Type = "shell"
            }
        };
        var executor = new TaskExecutor(tasks, captureOutput: false);

        var result = await executor.ExecuteTaskWithResultAsync("echo-test");

        Assert.True(result.Success);
        Assert.Null(result.StandardOutput);
    }

    [Fact]
    public async Task ExecuteTaskWithResultAsync_CapturesStderr()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["stderr-test"] = new TaskDefinition
            {
                Command = "echo 'Error message' >&2",
                Type = "shell"
            }
        };
        var executor = new TaskExecutor(tasks, captureOutput: true);

        var result = await executor.ExecuteTaskWithResultAsync("stderr-test");

        Assert.True(result.Success);
        Assert.NotNull(result.StandardError);
        Assert.Contains("Error message", result.StandardError);
    }

    [Fact]
    public async Task ExecuteTasksWithResultAsync_ReturnsAllResults()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["task1"] = new TaskDefinition { Command = "echo task1", Type = "shell" },
            ["task2"] = new TaskDefinition { Command = "echo task2", Type = "shell" },
            ["task3"] = new TaskDefinition { Command = "echo task3", Type = "shell" }
        };
        var executor = new TaskExecutor(tasks, captureOutput: true);

        var result = await executor.ExecuteTasksWithResultAsync(new[] { "task1", "task2", "task3" });

        Assert.True(result.Success);
        Assert.Equal(3, result.SucceededCount);
        Assert.Equal(3, result.Results.Count);
    }

    [Fact]
    public async Task ExecuteAliasWithResultAsync_ExecutesAllTasks()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["build"] = new TaskDefinition { Command = "echo build", Type = "shell" },
            ["test"] = new TaskDefinition { Command = "echo test", Type = "shell" }
        };
        var aliases = new Dictionary<string, string[]>
        {
            ["ci"] = new[] { "build", "test" }
        };
        var executor = new TaskExecutor(tasks, aliases: aliases, captureOutput: true);

        var result = await executor.ExecuteAliasWithResultAsync("ci");

        Assert.True(result.Success);
        Assert.Equal(2, result.Results.Count);
    }

    #endregion

    #region Security Validation Tests

    [Fact]
    public void SecurityValidator_ValidatesEnvValue_DetectsDangerousCharacters()
    {
        var validator = new SecurityValidator();

        var result = validator.ValidateEnvValue("TEST", "value;rm -rf /");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains(";"));
    }

    [Fact]
    public void SecurityValidator_ValidatesEnvValue_AllowsSafeValues()
    {
        var validator = new SecurityValidator();

        var result = validator.ValidateEnvValue("PATH", "/usr/bin:/usr/local/bin");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void SecurityValidator_ValidatesEnvValue_AllowsVariableReferences()
    {
        var validator = new SecurityValidator();

        var result = validator.ValidateEnvValue("CONFIG", "${env:HOME}/config");

        Assert.True(result.IsValid);
        Assert.True(result.HasWarnings); // Warning about variable reference is OK
    }

    [Fact]
    public void SecurityValidator_ValidatesCommand_DetectsDangerousPatterns()
    {
        var validator = new SecurityValidator();

        var result = validator.ValidateCommand("rm -rf /");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("rm -rf /"));
    }

    [Fact]
    public void SecurityValidator_ValidatesCommand_AllowsSafeCommands()
    {
        var validator = new SecurityValidator();

        var result = validator.ValidateCommand("dotnet build -c Release");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void SecurityValidator_ValidatesCommand_WarnsSudo()
    {
        var validator = new SecurityValidator();

        var result = validator.ValidateCommand("sudo apt-get update");

        Assert.True(result.IsValid); // Not blocked, just warned
        Assert.True(result.HasWarnings);
        Assert.Contains(result.Warnings, w => w.Contains("sudo"));
    }

    [Fact]
    public void SecurityValidator_ValidatesWorkingDirectory_ValidPath()
    {
        var validator = new SecurityValidator(projectRoot: "/home/user/project");

        var result = validator.ValidateWorkingDirectory("/home/user/project/src");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void SecurityValidator_ValidatesWorkingDirectory_OutsideProjectWarns()
    {
        var validator = new SecurityValidator(projectRoot: "/home/user/project");

        var result = validator.ValidateWorkingDirectory("/home/user/other");

        Assert.True(result.IsValid); // Warns but doesn't block
        Assert.True(result.HasWarnings);
    }

    [Fact]
    public void SecurityValidator_ValidateTask_CombinesAllChecks()
    {
        var validator = new SecurityValidator();
        var env = new Dictionary<string, string>
        {
            ["SAFE_VAR"] = "safe_value"
        };

        var result = validator.ValidateTask("test-task", "echo hello", null, env);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ExecuteTask_WithDangerousCommand_Fails()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["dangerous"] = new TaskDefinition
            {
                Command = "rm -rf /",
                Type = "shell"
            }
        };
        var executor = new TaskExecutor(tasks);

        var result = await executor.ExecuteTaskWithResultAsync("dangerous");

        Assert.False(result.Success);
    }

    #endregion

    #region Init Templates Tests

    [Fact]
    public void InitTemplates_HasAllTemplates()
    {
        var templates = InitTemplates.AvailableTemplates;

        Assert.Contains("dotnet", templates);
        Assert.Contains("node", templates);
        Assert.Contains("python", templates);
        Assert.Contains("docker", templates);
        Assert.Contains("default", templates);
    }

    [Fact]
    public void InitTemplates_GetTemplateJson_ReturnsValidJson()
    {
        foreach (var template in InitTemplates.AvailableTemplates)
        {
            var json = InitTemplates.GetTemplateJson(template);

            Assert.NotEmpty(json);
            Assert.Contains("\"version\":", json);
            Assert.Contains("\"tasks\":", json);
        }
    }

    [Fact]
    public void InitTemplates_GetTemplateYaml_ReturnsValidYaml()
    {
        foreach (var template in InitTemplates.AvailableTemplates)
        {
            var yaml = InitTemplates.GetTemplateYaml(template);

            Assert.NotEmpty(yaml);
            Assert.Contains("version:", yaml);
            Assert.Contains("tasks:", yaml);
        }
    }

    [Fact]
    public void InitTemplates_DotNetTemplate_HasExpectedTasks()
    {
        var json = InitTemplates.GetTemplateJson("dotnet");

        Assert.Contains("restore", json);
        Assert.Contains("build", json);
        Assert.Contains("test", json);
        Assert.Contains("clean", json);
        Assert.Contains("dotnet", json);
    }

    [Fact]
    public void InitTemplates_NodeTemplate_HasExpectedTasks()
    {
        var json = InitTemplates.GetTemplateJson("node");

        Assert.Contains("install", json);
        Assert.Contains("build", json);
        Assert.Contains("test", json);
        Assert.Contains("lint", json);
        Assert.Contains("npm", json);
    }

    [Fact]
    public void InitTemplates_PythonTemplate_HasExpectedTasks()
    {
        var json = InitTemplates.GetTemplateJson("python");

        Assert.Contains("venv", json);
        Assert.Contains("install", json);
        Assert.Contains("test", json);
        Assert.Contains("lint", json);
        Assert.Contains("pytest", json);
    }

    [Fact]
    public void InitTemplates_DockerTemplate_HasExpectedTasks()
    {
        var json = InitTemplates.GetTemplateJson("docker");

        Assert.Contains("build", json);
        Assert.Contains("run", json);
        Assert.Contains("push", json);
        Assert.Contains("docker", json);
    }

    [Fact]
    public void InitTemplates_UnknownTemplate_ReturnsDefault()
    {
        var unknownJson = InitTemplates.GetTemplateJson("unknown");
        var defaultJson = InitTemplates.GetTemplateJson("default");

        Assert.Equal(defaultJson, unknownJson);
    }

    #endregion

    #region TaskResult Output Properties Tests

    [Fact]
    public void TaskResult_Succeeded_IncludesOutput()
    {
        var result = TaskResult.Succeeded("test", TimeSpan.FromSeconds(1), "stdout content", "stderr content");

        Assert.Equal("stdout content", result.StandardOutput);
        Assert.Equal("stderr content", result.StandardError);
        Assert.Contains("stdout content", result.CombinedOutput!);
        Assert.Contains("stderr content", result.CombinedOutput!);
    }

    [Fact]
    public void TaskResult_Succeeded_NullOutput_NullCombined()
    {
        var result = TaskResult.Succeeded("test", TimeSpan.FromSeconds(1), null, null);

        Assert.Null(result.StandardOutput);
        Assert.Null(result.StandardError);
        Assert.Null(result.CombinedOutput);
    }

    [Fact]
    public void TaskResult_Succeeded_OnlyStdout_CombinedEqualsStdout()
    {
        var result = TaskResult.Succeeded("test", TimeSpan.FromSeconds(1), "stdout only", null);

        Assert.Equal("stdout only", result.StandardOutput);
        Assert.Null(result.StandardError);
        Assert.Equal("stdout only", result.CombinedOutput);
    }

    [Fact]
    public void TaskResult_Succeeded_OnlyStderr_CombinedEqualsStderr()
    {
        var result = TaskResult.Succeeded("test", TimeSpan.FromSeconds(1), null, "stderr only");

        Assert.Null(result.StandardOutput);
        Assert.Equal("stderr only", result.StandardError);
        Assert.Equal("stderr only", result.CombinedOutput);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task Integration_ExecuteMultipleTasks_WithOutputCapture()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rot-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var tasks = new Dictionary<string, TaskDefinition>
            {
                ["create-file"] = new TaskDefinition
                {
                    Command = $"echo 'test content' > {Path.Combine(tempDir, "test.txt")}",
                    Type = "shell"
                },
                ["read-file"] = new TaskDefinition
                {
                    Command = $"cat {Path.Combine(tempDir, "test.txt")}",
                    Type = "shell",
                    DependsOn = new[] { "create-file" }
                }
            };
            var executor = new TaskExecutor(tasks, captureOutput: true);

            var result = await executor.ExecuteTaskWithResultAsync("read-file");

            Assert.True(result.Success);
            Assert.NotNull(result.StandardOutput);
            Assert.Contains("test content", result.StandardOutput);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task Integration_ExecuteWithWorkingDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rot-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "test.txt"), "working dir content");

        try
        {
            var tasks = new Dictionary<string, TaskDefinition>
            {
                ["pwd-test"] = new TaskDefinition
                {
                    Command = "cat test.txt",
                    Type = "shell",
                    Cwd = tempDir
                }
            };
            var executor = new TaskExecutor(tasks, captureOutput: true);

            var result = await executor.ExecuteTaskWithResultAsync("pwd-test");

            Assert.True(result.Success);
            Assert.NotNull(result.StandardOutput);
            Assert.Contains("working dir content", result.StandardOutput);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task Integration_ExecuteWithVariableSubstitution_CapturesOutput()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["greet"] = new TaskDefinition
            {
                Command = "echo Hello, ${name}!",
                Type = "shell"
            }
        };
        var variables = new Dictionary<string, string>
        {
            ["name"] = "Integration Test"
        };
        var executor = new TaskExecutor(tasks, variables: variables, captureOutput: true);

        var result = await executor.ExecuteTaskWithResultAsync("greet");

        Assert.True(result.Success);
        Assert.NotNull(result.StandardOutput);
        Assert.Contains("Hello, Integration Test!", result.StandardOutput);
    }

    [Fact]
    public async Task Integration_FailingTask_CapturesStderr()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["fail"] = new TaskDefinition
            {
                Command = "echo 'Error occurred' >&2 && exit 1",
                Type = "shell"
            }
        };
        var executor = new TaskExecutor(tasks, captureOutput: true);

        var result = await executor.ExecuteTaskWithResultAsync("fail");

        Assert.False(result.Success);
        Assert.NotNull(result.StandardError);
        Assert.Contains("Error occurred", result.StandardError);
    }

    #endregion
}
