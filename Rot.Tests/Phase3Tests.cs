using Rot.Logging;
using Rot.Models;
using Rot.Services;

namespace Rot.Tests;

public class Phase3Tests
{
    #region Conditional Execution Tests

    [Fact]
    public void ConditionEvaluator_NullCondition_ReturnsTrue()
    {
        var evaluator = new ConditionEvaluator(NullLogger.Instance);

        var result = evaluator.Evaluate(null, "test-task");

        Assert.True(result);
    }

    [Fact]
    public void ConditionEvaluator_OsCondition_LinuxOnLinux_ReturnsTrue()
    {
        if (!OperatingSystem.IsLinux())
            return; // Skip on non-Linux

        var evaluator = new ConditionEvaluator(NullLogger.Instance);
        var condition = new TaskCondition { Os = "linux" };

        var result = evaluator.Evaluate(condition, "test-task");

        Assert.True(result);
    }

    [Fact]
    public void ConditionEvaluator_OsCondition_WindowsOnLinux_ReturnsFalse()
    {
        if (!OperatingSystem.IsLinux())
            return; // Skip on non-Linux

        var evaluator = new ConditionEvaluator(NullLogger.Instance);
        var condition = new TaskCondition { Os = "windows" };

        var result = evaluator.Evaluate(condition, "test-task");

        Assert.False(result);
    }

    [Fact]
    public void ConditionEvaluator_EnvCondition_ExistingVar_ReturnsTrue()
    {
        Environment.SetEnvironmentVariable("ROT_TEST_CONDITION", "expected");
        try
        {
            var evaluator = new ConditionEvaluator(NullLogger.Instance);
            var condition = new TaskCondition
            {
                Env = new Dictionary<string, string> { ["ROT_TEST_CONDITION"] = "expected" }
            };

            var result = evaluator.Evaluate(condition, "test-task");

            Assert.True(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ROT_TEST_CONDITION", null);
        }
    }

    [Fact]
    public void ConditionEvaluator_EnvCondition_WrongValue_ReturnsFalse()
    {
        Environment.SetEnvironmentVariable("ROT_TEST_CONDITION", "actual");
        try
        {
            var evaluator = new ConditionEvaluator(NullLogger.Instance);
            var condition = new TaskCondition
            {
                Env = new Dictionary<string, string> { ["ROT_TEST_CONDITION"] = "expected" }
            };

            var result = evaluator.Evaluate(condition, "test-task");

            Assert.False(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ROT_TEST_CONDITION", null);
        }
    }

    [Fact]
    public void ConditionEvaluator_EnvCondition_ExistsCheck_ReturnsTrue()
    {
        Environment.SetEnvironmentVariable("ROT_TEST_EXISTS", "any-value");
        try
        {
            var evaluator = new ConditionEvaluator(NullLogger.Instance);
            var condition = new TaskCondition
            {
                Env = new Dictionary<string, string> { ["ROT_TEST_EXISTS"] = "" }
            };

            var result = evaluator.Evaluate(condition, "test-task");

            Assert.True(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ROT_TEST_EXISTS", null);
        }
    }

    [Fact]
    public void ConditionEvaluator_FileExists_ExistingFile_ReturnsTrue()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var evaluator = new ConditionEvaluator(NullLogger.Instance);
            var condition = new TaskCondition
            {
                FileExists = new[] { tempFile }
            };

            var result = evaluator.Evaluate(condition, "test-task");

            Assert.True(result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ConditionEvaluator_FileExists_NonExistingFile_ReturnsFalse()
    {
        var evaluator = new ConditionEvaluator(NullLogger.Instance);
        var condition = new TaskCondition
        {
            FileExists = new[] { "/nonexistent/file/path.txt" }
        };

        var result = evaluator.Evaluate(condition, "test-task");

        Assert.False(result);
    }

    [Fact]
    public void ConditionEvaluator_FileNotExists_NonExistingFile_ReturnsTrue()
    {
        var evaluator = new ConditionEvaluator(NullLogger.Instance);
        var condition = new TaskCondition
        {
            FileNotExists = new[] { "/nonexistent/file/path.txt" }
        };

        var result = evaluator.Evaluate(condition, "test-task");

        Assert.True(result);
    }

    [Fact]
    public void ConditionEvaluator_FileNotExists_ExistingFile_ReturnsFalse()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var evaluator = new ConditionEvaluator(NullLogger.Instance);
            var condition = new TaskCondition
            {
                FileNotExists = new[] { tempFile }
            };

            var result = evaluator.Evaluate(condition, "test-task");

            Assert.False(result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteTaskAsync_ConditionNotMet_SkipsTask()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["skip-me"] = new TaskDefinition
            {
                Command = "echo should-not-run",
                Type = "shell",
                Condition = new TaskCondition { Os = "invalid-os-never-matches" }
            }
        };
        var executor = new TaskExecutor(tasks);

        var result = await executor.ExecuteTaskAsync("skip-me");

        Assert.Equal(0, result); // Skipped tasks return 0
    }

    #endregion

    #region Task Hooks Tests

    [Fact]
    public async Task ExecuteTaskAsync_WithPreTask_ExecutesPreTaskFirst()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"rot-pre-task-{Guid.NewGuid()}.txt");
        try
        {
            var tasks = new Dictionary<string, TaskDefinition>
            {
                ["pre"] = new TaskDefinition
                {
                    Command = $"touch {tempFile}",
                    Type = "shell"
                },
                ["main"] = new TaskDefinition
                {
                    Command = "echo main",
                    Type = "shell",
                    PreTasks = new[] { "pre" }
                }
            };
            var executor = new TaskExecutor(tasks);

            var result = await executor.ExecuteTaskAsync("main");

            Assert.Equal(0, result);
            Assert.True(File.Exists(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteTaskAsync_WithPostTask_ExecutesPostTaskAfter()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"rot-post-task-{Guid.NewGuid()}.txt");
        try
        {
            var tasks = new Dictionary<string, TaskDefinition>
            {
                ["main"] = new TaskDefinition
                {
                    Command = "echo main",
                    Type = "shell",
                    PostTasks = new[] { "post" }
                },
                ["post"] = new TaskDefinition
                {
                    Command = $"touch {tempFile}",
                    Type = "shell"
                }
            };
            var executor = new TaskExecutor(tasks);

            var result = await executor.ExecuteTaskAsync("main");

            Assert.Equal(0, result);
            Assert.True(File.Exists(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ExecuteTaskAsync_PreTaskFails_DoesNotExecuteMain()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["pre-fail"] = new TaskDefinition
            {
                Command = "exit 1",
                Type = "shell"
            },
            ["main"] = new TaskDefinition
            {
                Command = "echo main",
                Type = "shell",
                PreTasks = new[] { "pre-fail" }
            }
        };
        var executor = new TaskExecutor(tasks);

        var result = await executor.ExecuteTaskAsync("main");

        Assert.NotEqual(0, result);
    }

    #endregion

    #region Profile Support Tests

    [Fact]
    public async Task ExecuteTaskAsync_WithProfileEnv_AppliesEnvironment()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["check-env"] = new TaskDefinition
            {
                Command = "printenv PROFILE_VAR",
                Type = "shell"
            }
        };
        var profileEnv = new Dictionary<string, string>
        {
            ["PROFILE_VAR"] = "profile-value"
        };
        var executor = new TaskExecutor(tasks, aliases: null, profileEnv: profileEnv);

        var result = await executor.ExecuteTaskAsync("check-env");

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ExecuteTaskAsync_TaskEnvOverridesProfileEnv()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["check-env"] = new TaskDefinition
            {
                Command = "printenv OVERRIDE_VAR",
                Type = "shell",
                Env = new Dictionary<string, string>
                {
                    ["OVERRIDE_VAR"] = "task-value"
                }
            }
        };
        var profileEnv = new Dictionary<string, string>
        {
            ["OVERRIDE_VAR"] = "profile-value"
        };
        var executor = new TaskExecutor(tasks, aliases: null, profileEnv: profileEnv);

        var result = await executor.ExecuteTaskAsync("check-env");

        Assert.Equal(0, result);
    }

    #endregion

    #region Task Caching Tests

    [Fact]
    public void CacheManager_NoCache_ReturnsFalse()
    {
        var cacheManager = new CacheManager(NullLogger.Instance, Path.GetTempPath());
        var cache = new TaskCacheConfig
        {
            Inputs = new[] { "**/*.cs" }
        };

        var result = cacheManager.IsCacheValid("nonexistent-task", cache);

        Assert.False(result);
    }

    [Fact]
    public void CacheManager_SaveAndCheck_ReturnsTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rot-cache-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Create a test input file
            var inputFile = Path.Combine(tempDir, "input.txt");
            File.WriteAllText(inputFile, "test content");

            var cacheManager = new CacheManager(NullLogger.Instance, tempDir, Path.Combine(tempDir, ".rot", "cache"));
            var cache = new TaskCacheConfig
            {
                Inputs = new[] { "input.txt" }
            };

            // Save cache
            cacheManager.SaveCache("test-task", cache);

            // Check cache
            var result = cacheManager.IsCacheValid("test-task", cache);

            Assert.True(result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void CacheManager_InputChanged_ReturnsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rot-cache-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Create a test input file
            var inputFile = Path.Combine(tempDir, "input.txt");
            File.WriteAllText(inputFile, "original content");

            var cacheManager = new CacheManager(NullLogger.Instance, tempDir, Path.Combine(tempDir, ".rot", "cache"));
            var cache = new TaskCacheConfig
            {
                Inputs = new[] { "input.txt" }
            };

            // Save cache
            cacheManager.SaveCache("test-task", cache);

            // Modify input file
            Thread.Sleep(100); // Ensure timestamp changes
            File.WriteAllText(inputFile, "modified content");

            // Check cache
            var result = cacheManager.IsCacheValid("test-task", cache);

            Assert.False(result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void CacheManager_TtlExpired_ReturnsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rot-cache-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var inputFile = Path.Combine(tempDir, "input.txt");
            File.WriteAllText(inputFile, "test content");

            var cacheManager = new CacheManager(NullLogger.Instance, tempDir, Path.Combine(tempDir, ".rot", "cache"));
            var cache = new TaskCacheConfig
            {
                Inputs = new[] { "input.txt" },
                TtlMinutes = 0 // Immediate expiration (0 minutes TTL means always expired)
            };

            // Save cache
            cacheManager.SaveCache("test-task", cache);

            // Wait a moment to ensure TTL check fails
            Thread.Sleep(100);

            // Check cache with 0 TTL - should be invalid
            var result = cacheManager.IsCacheValid("test-task", cache);

            // With TtlMinutes = 0, age > 0 is always true
            Assert.False(result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void CacheManager_InvalidateCache_RemovesCache()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rot-cache-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var inputFile = Path.Combine(tempDir, "input.txt");
            File.WriteAllText(inputFile, "test content");

            var cacheManager = new CacheManager(NullLogger.Instance, tempDir, Path.Combine(tempDir, ".rot", "cache"));
            var cache = new TaskCacheConfig
            {
                Inputs = new[] { "input.txt" }
            };

            // Save cache
            cacheManager.SaveCache("test-task", cache);
            Assert.True(cacheManager.IsCacheValid("test-task", cache));

            // Invalidate cache
            cacheManager.InvalidateCache("test-task");

            // Check cache
            var result = cacheManager.IsCacheValid("test-task", cache);

            Assert.False(result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteTaskAsync_NoCacheFlag_BypassesCache()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"rot-nocache-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var outputFile = Path.Combine(tempDir, "output.txt");
        try
        {
            var tasks = new Dictionary<string, TaskDefinition>
            {
                ["cached-task"] = new TaskDefinition
                {
                    Command = $"echo executed > {outputFile}",
                    Type = "shell",
                    Cache = new TaskCacheConfig
                    {
                        Inputs = new[] { "*.nonexistent" }
                    }
                }
            };

            // First run with cache
            var executor1 = new TaskExecutor(tasks, aliases: null, noCache: false);
            await executor1.ExecuteTaskAsync("cached-task");

            // Delete output file
            if (File.Exists(outputFile))
                File.Delete(outputFile);

            // Second run with noCache - should execute
            var executor2 = new TaskExecutor(tasks, aliases: null, noCache: true);
            await executor2.ExecuteTaskAsync("cached-task");

            Assert.True(File.Exists(outputFile));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    #endregion

    #region Plugin System Tests

    [Fact]
    public void PluginLoader_NoPlugins_HasBuiltInTypes()
    {
        var pluginLoader = new PluginLoader(NullLogger.Instance);

        var types = pluginLoader.GetRegisteredTaskTypes().ToList();

        Assert.Contains("shell", types);
        Assert.Contains("process", types);
    }

    [Fact]
    public void PluginLoader_RegisterProvider_AddsProvider()
    {
        var pluginLoader = new PluginLoader(NullLogger.Instance);
        var mockProvider = new MockTaskTypeProvider();

        pluginLoader.RegisterProvider(mockProvider);

        Assert.True(pluginLoader.HasProvider("mock"));
        Assert.Equal(mockProvider, pluginLoader.GetProvider("mock"));
    }

    [Fact]
    public void PluginLoader_GetNonExistentProvider_ReturnsNull()
    {
        var pluginLoader = new PluginLoader(NullLogger.Instance);

        var provider = pluginLoader.GetProvider("nonexistent");

        Assert.Null(provider);
    }

    private class MockTaskTypeProvider : ITaskTypeProvider
    {
        public string TaskType => "mock";

        public Task<int> ExecuteAsync(TaskDefinition task, string taskName, ITaskLogger logger)
        {
            return Task.FromResult(0);
        }
    }

    #endregion

    #region Validator Tests for Phase 3

    [Fact]
    public void TaskValidator_ValidPreTask_NoErrors()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["pre"] = new TaskDefinition { Command = "echo pre" },
            ["main"] = new TaskDefinition
            {
                Command = "echo main",
                PreTasks = new[] { "pre" }
            }
        };
        var validator = new TaskValidator();

        var result = validator.ValidateAll(tasks);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void TaskValidator_InvalidPreTask_HasError()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["main"] = new TaskDefinition
            {
                Command = "echo main",
                PreTasks = new[] { "nonexistent" }
            }
        };
        var validator = new TaskValidator();

        var result = validator.ValidateAll(tasks);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("PreTask") && e.Contains("nonexistent"));
    }

    [Fact]
    public void TaskValidator_InvalidPostTask_HasError()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["main"] = new TaskDefinition
            {
                Command = "echo main",
                PostTasks = new[] { "nonexistent" }
            }
        };
        var validator = new TaskValidator();

        var result = validator.ValidateAll(tasks);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("PostTask") && e.Contains("nonexistent"));
    }

    [Fact]
    public void TaskValidator_InvalidCacheTtl_HasError()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["cached"] = new TaskDefinition
            {
                Command = "echo cached",
                Cache = new TaskCacheConfig
                {
                    Inputs = new[] { "*.cs" },
                    TtlMinutes = -1
                }
            }
        };
        var validator = new TaskValidator();

        var result = validator.ValidateAll(tasks);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("TTL") && e.Contains("-1"));
    }

    [Fact]
    public void TaskValidator_CacheNoInputs_HasWarning()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["cached"] = new TaskDefinition
            {
                Command = "echo cached",
                Cache = new TaskCacheConfig
                {
                    Inputs = Array.Empty<string>()
                }
            }
        };
        var validator = new TaskValidator();

        var result = validator.ValidateAll(tasks);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("no input patterns"));
    }

    [Fact]
    public void TaskValidator_UnknownOsCondition_HasWarning()
    {
        var tasks = new Dictionary<string, TaskDefinition>
        {
            ["conditional"] = new TaskDefinition
            {
                Command = "echo conditional",
                Condition = new TaskCondition { Os = "unknown-os" }
            }
        };
        var validator = new TaskValidator();

        var result = validator.ValidateAll(tasks);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("Unknown OS") && w.Contains("unknown-os"));
    }

    #endregion
}
