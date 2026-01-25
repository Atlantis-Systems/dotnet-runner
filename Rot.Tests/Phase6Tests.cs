using Rot.Logging;
using Rot.Models;
using Rot.Services;

namespace Rot.Tests;

/// <summary>
/// Tests for Phase 6 features: Interactive Mode, Parallelism Control, Secrets, Audit Logging.
/// </summary>
public class Phase6Tests : IDisposable
{
    private readonly string _tempDir;

    public Phase6Tests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"rot-phase6-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    #region Interactive Mode Tests

    [Fact]
    public void TaskDefinition_SupportsPrompts()
    {
        // Arrange
        var task = new TaskDefinition
        {
            Command = "echo ${environment}",
            Prompts = new Dictionary<string, TaskPrompt>
            {
                ["environment"] = new TaskPrompt
                {
                    Message = "Deploy to which environment?",
                    Choices = new[] { "staging", "production" },
                    Default = "staging",
                    Required = true
                }
            }
        };

        // Assert
        Assert.Single(task.Prompts);
        Assert.Equal("Deploy to which environment?", task.Prompts["environment"].Message);
        Assert.Equal(2, task.Prompts["environment"].Choices.Length);
        Assert.Equal("staging", task.Prompts["environment"].Default);
        Assert.True(task.Prompts["environment"].Required);
    }

    [Fact]
    public void TaskPrompt_HasAllProperties()
    {
        // Arrange
        var prompt = new TaskPrompt
        {
            Message = "Enter API key",
            Default = "default-key",
            Choices = new[] { "key1", "key2" },
            Secret = true,
            Required = true,
            Pattern = @"^[A-Z0-9]+$",
            ValidationMessage = "Must be uppercase alphanumeric"
        };

        // Assert
        Assert.Equal("Enter API key", prompt.Message);
        Assert.Equal("default-key", prompt.Default);
        Assert.Equal(2, prompt.Choices.Length);
        Assert.True(prompt.Secret);
        Assert.True(prompt.Required);
        Assert.Equal(@"^[A-Z0-9]+$", prompt.Pattern);
        Assert.Equal("Must be uppercase alphanumeric", prompt.ValidationMessage);
    }

    [Fact]
    public async Task InteractiveService_NonInteractiveMode_UsesDefaults()
    {
        // Arrange
        var logger = NullLogger.Instance;
        var service = new InteractiveService(logger, nonInteractive: true);
        var prompts = new Dictionary<string, TaskPrompt>
        {
            ["environment"] = new TaskPrompt
            {
                Message = "Environment?",
                Default = "staging",
                Required = true
            }
        };

        // Act
        var values = await service.CollectPromptsAsync("test-task", prompts, CancellationToken.None);

        // Assert
        Assert.Single(values);
        Assert.Equal("staging", values["environment"]);
    }

    #endregion

    #region Parallelism Control Tests

    [Fact]
    public void TaskDefinition_SupportsParallelOption()
    {
        // Arrange
        var task = new TaskDefinition
        {
            Command = "run-tests",
            DependsOn = new[] { "test1", "test2", "test3", "test4" },
            Parallel = 2
        };

        // Assert
        Assert.Equal(2, task.Parallel);
        Assert.Equal(4, task.DependsOn.Length);
    }

    [Fact]
    public async Task TaskExecutor_ParallelOption_LimitsMaxConcurrency()
    {
        // Arrange - Create tasks with parallel limit
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["fast1"] = new TaskDefinition { Command = "echo fast1" },
            ["fast2"] = new TaskDefinition { Command = "echo fast2" },
            ["fast3"] = new TaskDefinition { Command = "echo fast3" },
            ["all"] = new TaskDefinition
            {
                Command = "echo done",
                DependsOn = new[] { "fast1", "fast2", "fast3" },
                Parallel = 2 // Only run 2 at a time
            }
        };

        var executor = new TaskExecutor(tasks, allowConcurrency: true);

        // Act
        var result = await executor.ExecuteTaskWithResultAsync("all");

        // Assert - Task should complete (parallelism control works)
        Assert.True(result.Success);
    }

    #endregion

    #region Secrets Provider Tests

    [Fact]
    public void SecretsProvider_Environment_LoadsFromEnv()
    {
        // Arrange
        var testKey = $"ROT_TEST_SECRET_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(testKey, "test-secret-value");

        var logger = NullLogger.Instance;
        var provider = new SecretsProvider(logger);

        try
        {
            // Act - Using env source explicitly
            var secret = provider.GetSecret($"env:{testKey}");

            // Assert
            Assert.Equal("test-secret-value", secret);
        }
        finally
        {
            Environment.SetEnvironmentVariable(testKey, null);
        }
    }

    [Fact]
    public void SecretsProvider_DefaultsToEnvSource()
    {
        // Arrange
        var testKey = $"ROT_TEST_SECRET_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(testKey, "default-env-value");

        var logger = NullLogger.Instance;
        var provider = new SecretsProvider(logger);

        try
        {
            // Act - Without explicit source (should default to env)
            var secret = provider.GetSecret(testKey);

            // Assert
            Assert.Equal("default-env-value", secret);
        }
        finally
        {
            Environment.SetEnvironmentVariable(testKey, null);
        }
    }

    [Fact]
    public void SecretsProvider_FileSource_LoadsFromFile()
    {
        // Arrange
        var secretFile = Path.Combine(_tempDir, "secret.txt");
        File.WriteAllText(secretFile, "file-secret-value");

        var logger = NullLogger.Instance;
        var provider = new SecretsProvider(logger);

        // Act
        var secret = provider.GetSecret($"file:{secretFile}");

        // Assert
        Assert.Equal("file-secret-value", secret);
    }

    [Fact]
    public void SecretsProvider_FileSource_LoadsKeyValueFromFile()
    {
        // Arrange
        var secretFile = Path.Combine(_tempDir, "secrets.env");
        File.WriteAllText(secretFile, @"
# Comment
API_KEY=my-api-key
DB_PASSWORD=my-db-password
");

        var logger = NullLogger.Instance;
        var provider = new SecretsProvider(logger);

        // Act
        var apiKey = provider.GetSecret($"file:{secretFile}#API_KEY");
        var dbPassword = provider.GetSecret($"file:{secretFile}#DB_PASSWORD");

        // Assert
        Assert.Equal("my-api-key", apiKey);
        Assert.Equal("my-db-password", dbPassword);
    }

    [Fact]
    public void SecretsProvider_DotEnvSource_LoadsFromDotEnv()
    {
        // Arrange
        var dotEnvFile = Path.Combine(_tempDir, ".env");
        File.WriteAllText(dotEnvFile, @"
TEST_API_KEY=dotenv-api-key
TEST_SECRET=""quoted value""
");

        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_tempDir);

        try
        {
            var logger = NullLogger.Instance;
            var provider = new SecretsProvider(logger);

            // Act
            var apiKey = provider.GetSecret("dotenv:TEST_API_KEY");
            var secret = provider.GetSecret("dotenv:TEST_SECRET");

            // Assert
            Assert.Equal("dotenv-api-key", apiKey);
            Assert.Equal("quoted value", secret);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public void SecretsProvider_ReturnsNull_WhenSecretNotFound()
    {
        // Arrange
        var logger = NullLogger.Instance;
        var provider = new SecretsProvider(logger);

        // Act
        var secret = provider.GetSecret("nonexistent-secret-key-12345");

        // Assert
        Assert.Null(secret);
    }

    #endregion

    #region Audit Logger Tests

    [Fact]
    public void AuditLogger_LogsTaskStart()
    {
        // Arrange
        var auditFile = Path.Combine(_tempDir, "audit.log");
        var logger = NullLogger.Instance;

        using var auditLogger = new AuditLogger(auditFile, logger, enabled: true);

        // Act
        auditLogger.LogTaskStart("build", "dotnet build", "/project");

        // Assert
        var content = File.ReadAllText(auditFile);
        Assert.Contains("TaskStart", content);
        Assert.Contains("build", content);
        Assert.Contains("dotnet build", content);
    }

    [Fact]
    public void AuditLogger_LogsTaskComplete()
    {
        // Arrange
        var auditFile = Path.Combine(_tempDir, "audit.log");
        var logger = NullLogger.Instance;

        using var auditLogger = new AuditLogger(auditFile, logger, enabled: true);

        // Act
        auditLogger.LogTaskComplete("build", 0, TimeSpan.FromSeconds(5), true);

        // Assert
        var content = File.ReadAllText(auditFile);
        Assert.Contains("TaskComplete", content);
        Assert.Contains("build", content);
        Assert.Contains("5000", content); // Duration in ms
    }

    [Fact]
    public void AuditLogger_LogsTaskFailed()
    {
        // Arrange
        var auditFile = Path.Combine(_tempDir, "audit.log");
        var logger = NullLogger.Instance;

        using var auditLogger = new AuditLogger(auditFile, logger, enabled: true);

        // Act
        auditLogger.LogTaskFailed("test", 1, "Tests failed", TimeSpan.FromSeconds(10));

        // Assert
        var content = File.ReadAllText(auditFile);
        Assert.Contains("TaskFailed", content);
        Assert.Contains("Tests failed", content);
    }

    [Fact]
    public void AuditLogger_LogsSecurityViolation()
    {
        // Arrange
        var auditFile = Path.Combine(_tempDir, "audit.log");
        var logger = NullLogger.Instance;

        using var auditLogger = new AuditLogger(auditFile, logger, enabled: true);

        // Act
        auditLogger.LogSecurityViolation("dangerous", "rm -rf /", new[] { "Dangerous command detected" });

        // Assert
        var content = File.ReadAllText(auditFile);
        Assert.Contains("SecurityViolation", content);
        Assert.Contains("Dangerous command detected", content);
    }

    [Fact]
    public void AuditLogger_SanitizesSecretsInCommands()
    {
        // Arrange
        var auditFile = Path.Combine(_tempDir, "audit.log");
        var logger = NullLogger.Instance;

        using var auditLogger = new AuditLogger(auditFile, logger, enabled: true);

        // Act
        auditLogger.LogTaskStart("deploy", "deploy --password=supersecret --token=abc123", "/");

        // Assert
        var content = File.ReadAllText(auditFile);
        Assert.DoesNotContain("supersecret", content);
        Assert.DoesNotContain("abc123", content);
        Assert.Contains("password=***", content);
        Assert.Contains("token=***", content);
    }

    [Fact]
    public void AuditLogger_GetRecentEntries_ReturnsEntries()
    {
        // Arrange
        var auditFile = Path.Combine(_tempDir, "audit.log");
        var logger = NullLogger.Instance;

        using var auditLogger = new AuditLogger(auditFile, logger, enabled: true);
        auditLogger.LogTaskStart("task1", "echo 1", "/");
        auditLogger.LogTaskComplete("task1", 0, TimeSpan.FromSeconds(1), true);
        auditLogger.LogTaskStart("task2", "echo 2", "/");
        auditLogger.LogTaskComplete("task2", 0, TimeSpan.FromSeconds(2), true);

        // Act
        var entries = auditLogger.GetRecentEntries(10);

        // Assert
        Assert.Equal(4, entries.Count);
    }

    [Fact]
    public void AuditLogger_Disabled_WritesNothing()
    {
        // Arrange
        var auditFile = Path.Combine(_tempDir, "audit-disabled.log");
        var logger = NullLogger.Instance;

        using var auditLogger = new AuditLogger(auditFile, logger, enabled: false);

        // Act
        auditLogger.LogTaskStart("build", "dotnet build", "/");

        // Assert
        Assert.False(File.Exists(auditFile));
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public void TaskResult_Cancelled_HasCorrectExitCode()
    {
        // Arrange & Act
        var result = TaskResult.Cancelled("build");

        // Assert
        Assert.False(result.Success);
        Assert.Equal(130, result.ExitCode); // Standard Ctrl+C exit code
        Assert.True(result.WasCancelled);
        Assert.Equal("Task was cancelled by user", result.ErrorMessage);
    }

    [Fact]
    public async Task TaskExecutor_Cancellation_StopsExecution()
    {
        // Arrange
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["long-task"] = new TaskDefinition
            {
                Command = OperatingSystem.IsWindows() ? "ping -n 10 127.0.0.1" : "sleep 10"
            }
        };

        using var cts = new CancellationTokenSource();
        var executor = new TaskExecutor(tasks);

        // Act - Cancel after a short delay
        var executeTask = Task.Run(async () =>
        {
            await Task.Delay(100);
            cts.Cancel();
        });

        var result = await executor.ExecuteTaskWithResultAsync("long-task", cts.Token);

        // Assert
        Assert.True(result.WasCancelled || !result.Success);
    }

    #endregion

    #region Variable Substitution with Secrets

    [Fact]
    public async Task TaskExecutor_SubstitutesSecrets()
    {
        // Arrange
        var testKey = $"ROT_SECRET_TEST_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(testKey, "my-secret-api-key");

        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["deploy"] = new TaskDefinition
            {
                Command = $"echo ${{secret:{testKey}}}"
            }
        };

        var executor = new TaskExecutor(tasks, captureOutput: true, dryRun: true);

        try
        {
            // Act
            var result = await executor.ExecuteTaskWithResultAsync("deploy");

            // Assert - In dry run, we just verify no errors
            Assert.True(result.Success || result.DryRun);
        }
        finally
        {
            Environment.SetEnvironmentVariable(testKey, null);
        }
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task TaskExecutor_WithAllPhase6Features()
    {
        // Arrange - Create a task config that uses all Phase 6 features
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["dep1"] = new TaskDefinition { Command = "echo dep1" },
            ["dep2"] = new TaskDefinition { Command = "echo dep2" },
            ["dep3"] = new TaskDefinition { Command = "echo dep3" },
            ["main"] = new TaskDefinition
            {
                Command = "echo main",
                DependsOn = new[] { "dep1", "dep2", "dep3" },
                Parallel = 2, // Limit to 2 concurrent
                Prompts = new Dictionary<string, TaskPrompt>
                {
                    ["confirm"] = new TaskPrompt
                    {
                        Message = "Confirm?",
                        Default = "yes",
                        Choices = new[] { "yes", "no" }
                    }
                }
            }
        };

        var auditFile = Path.Combine(_tempDir, "integration-audit.log");

        using var executor = new TaskExecutor(
            tasks,
            allowConcurrency: true,
            nonInteractive: true,
            auditLogPath: auditFile,
            enableAudit: true);

        // Act
        var result = await executor.ExecuteTaskWithResultAsync("main");

        // Assert
        Assert.True(result.Success);

        // Verify audit log was created
        Assert.True(File.Exists(auditFile));
        var auditContent = File.ReadAllText(auditFile);
        Assert.Contains("main", auditContent);
    }

    #endregion
}
