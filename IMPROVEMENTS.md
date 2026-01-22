# Rot - Recommended Improvements

This document outlines recommended improvements for the Rot task runner, organized by category and priority.

## Table of Contents

1. [Feature Improvements](#feature-improvements)
2. [Code Quality Improvements](#code-quality-improvements)
3. [Testing & Reliability](#testing--reliability)
4. [Developer Experience](#developer-experience)
5. [Performance Optimizations](#performance-optimizations)
6. [Security Enhancements](#security-enhancements)

---

## Feature Improvements

### High Priority

#### 1. Task Timeout Support
Add configurable timeouts to prevent runaway tasks from hanging indefinitely.

```csharp
// TaskDefinition.cs
public int? Timeout { get; set; } // Timeout in seconds
```

```yaml
tasks:
  long-running:
    command: npm run build
    timeout: 300  # 5 minutes
```

#### 2. Task Groups and Batch Execution
Allow running multiple tasks by group or pattern.

```bash
rot run --group build        # Run all tasks in "build" group
rot run --pattern "test-*"   # Run all tasks matching pattern
```

```csharp
// TaskDefinition.cs
public string Group { get; set; } = string.Empty;
public string[] Tags { get; set; } = Array.Empty<string>();
```

#### 3. Watch Mode
Re-run tasks automatically when files change.

```bash
rot watch build --glob "src/**/*.cs"
rot watch test --glob "**/*.cs" --debounce 500
```

#### 4. Dry Run Mode
Preview what would be executed without actually running commands.

```bash
rot run build --dry-run
# Output: Would execute: dotnet build
#         Working directory: /project
#         Environment: DEBUG=true
```

#### 5. Task Output Capture
Capture and store task output for later inspection or piping.

```bash
rot run build --output build.log
rot run build --json  # Output results as JSON
```

### Medium Priority

#### 6. Conditional Task Execution
Run tasks based on conditions (file existence, environment, OS).

```yaml
tasks:
  windows-build:
    command: msbuild
    condition:
      os: windows

  deploy:
    command: ./deploy.sh
    condition:
      env: CI=true
      fileExists: dist/bundle.js
```

#### 7. Task Hooks (Pre/Post)
Execute commands before and after tasks.

```yaml
tasks:
  build:
    command: dotnet build
    preTasks: [clean]
    postTasks: [notify]
```

#### 8. Variable Substitution
Support variables and interpolation in commands.

```yaml
variables:
  outputDir: ./dist
  config: Release

tasks:
  build:
    command: dotnet build -c ${config} -o ${outputDir}
```

#### 9. Interactive Mode
Prompt for input during task execution.

```yaml
tasks:
  deploy:
    command: ./deploy.sh ${environment}
    prompts:
      environment:
        message: "Deploy to which environment?"
        choices: [staging, production]
```

#### 10. Task Aliases
Create shortcuts for common task combinations.

```yaml
aliases:
  ci: [clean, build, test, pack]
  dev: [restore, build, watch]
```

```bash
rot ci  # Runs clean, build, test, pack in sequence
```

### Lower Priority

#### 11. Remote Task Execution
Execute tasks on remote machines via SSH.

```yaml
tasks:
  deploy-prod:
    command: systemctl restart app
    remote:
      host: prod-server.example.com
      user: deploy
```

#### 12. Task Parallelism Control
Fine-grained control over parallel execution.

```yaml
tasks:
  test-all:
    dependsOn: [test-unit, test-integration, test-e2e]
    parallel: 2  # Run max 2 dependencies at once
```

#### 13. Task Result Caching
Cache task results based on input file hashes.

```yaml
tasks:
  build:
    command: dotnet build
    cache:
      inputs: ["**/*.cs", "*.csproj"]
      outputs: ["bin/", "obj/"]
```

#### 14. Profile Support
Define different configurations for different environments.

```yaml
profiles:
  dev:
    env:
      DEBUG: "true"
  prod:
    env:
      OPTIMIZE: "true"
```

```bash
rot run build --profile prod
```

#### 15. Plugin System
Allow extending Rot with custom task types.

```yaml
plugins:
  - rot-plugin-docker
  - rot-plugin-kubernetes

tasks:
  deploy:
    type: docker
    image: myapp:latest
```

---

## Code Quality Improvements

### High Priority

#### 1. Separate Concerns in TaskExecutor
Split `TaskExecutor.cs` (252 lines) into focused classes:

```
Services/
├── TaskExecutor.cs        # Core execution orchestration
├── TaskLoader.cs          # Configuration loading (JSON/YAML)
├── ProcessRunner.cs       # Process execution logic
├── ConsoleFormatter.cs    # Colored output formatting
└── DependencyResolver.cs  # Dependency graph resolution
```

#### 2. Add Structured Logging
Replace `Console.WriteLine` with a proper logging abstraction.

```csharp
public class TaskExecutor
{
    private readonly ILogger<TaskExecutor> _logger;

    // Use log levels: Debug, Info, Warning, Error
    _logger.LogInformation("Executing task {TaskName}", taskName);
    _logger.LogDebug("Process started with PID {ProcessId}", process.Id);
}
```

Support log output destinations:
- Console (default)
- File
- Structured JSON

#### 3. Configuration Validation
Add validation for task configurations with helpful error messages.

```csharp
public class TaskValidator
{
    public ValidationResult Validate(TaskDefinition task)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(task.Command))
            errors.Add("Task 'command' is required");

        if (task.Type != "shell" && task.Type != "process")
            errors.Add($"Invalid task type '{task.Type}'. Use 'shell' or 'process'");

        if (task.Timeout.HasValue && task.Timeout.Value <= 0)
            errors.Add("Timeout must be a positive number");

        return new ValidationResult(errors);
    }
}
```

#### 4. Improve Error Messages
Provide actionable error messages with context.

```
Current:  "Task 'build' not found."
Improved: "Task 'build' not found in tasks.yaml.
          Available tasks: clean, test, deploy
          Did you mean 'rebuild'?"
```

#### 5. Add Async Cancellation Support
Support graceful shutdown with `CancellationToken`.

```csharp
public async Task<int> ExecuteTaskAsync(string taskName, CancellationToken ct = default)
{
    // ...
    await process.WaitForExitAsync(ct);
    // Handle Ctrl+C gracefully
}
```

### Medium Priority

#### 6. Use Result Pattern for Error Handling
Replace exit codes with a proper result type.

```csharp
public record TaskResult(
    bool Success,
    int ExitCode,
    string TaskName,
    TimeSpan Duration,
    string? ErrorMessage = null
);

public async Task<TaskResult> ExecuteTaskAsync(string taskName)
{
    // Return rich result instead of just exit code
}
```

#### 7. Add XML Documentation
Document public APIs for IntelliSense support.

```csharp
/// <summary>
/// Executes a task and all its dependencies.
/// </summary>
/// <param name="taskName">The name of the task to execute.</param>
/// <returns>Exit code (0 for success, non-zero for failure).</returns>
/// <exception cref="TaskNotFoundException">Task does not exist.</exception>
public async Task<int> ExecuteTaskAsync(string taskName)
```

#### 8. Implement IDisposable
Properly dispose of resources.

```csharp
public class TaskExecutor : IDisposable
{
    private readonly SemaphoreSlim _executionSemaphore = new(1, 1);
    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            _executionSemaphore.Dispose();
            _disposed = true;
        }
    }
}
```

---

## Testing & Reliability

### High Priority

#### 1. Add Unit Tests
Create comprehensive unit tests for core functionality.

```
Rot.Tests/
├── TaskExecutorTests.cs
├── TaskLoaderTests.cs
├── TaskValidatorTests.cs
├── DependencyResolverTests.cs
└── Fixtures/
    ├── valid-tasks.json
    ├── invalid-tasks.json
    └── circular-deps.yaml
```

**Test Coverage Goals:**
- TaskExecutor: Dependency resolution, circular detection, execution order
- TaskLoader: JSON parsing, YAML parsing, error handling
- CLI: Argument parsing, command routing

#### 2. Add Integration Tests
Test actual command execution in isolated environments.

```csharp
[Fact]
public async Task ExecuteTask_RunsShellCommand_ReturnsExitCode()
{
    var tempDir = CreateTempDirectory();
    var tasksFile = CreateTasksFile(tempDir, new { build = new { command = "echo hello" } });

    var executor = TaskExecutor.LoadFromFile(tasksFile);
    var result = await executor.ExecuteTaskAsync("build");

    Assert.Equal(0, result);
}
```

#### 3. Add CI Test Execution
Update GitHub Actions to actually run tests.

```yaml
# ci.yml
- name: Run tests
  run: dotnet test --configuration Release --logger "trx" --results-directory TestResults

- name: Upload test results
  uses: actions/upload-artifact@v3
  with:
    name: test-results
    path: TestResults/
```

### Medium Priority

#### 4. Add Benchmark Tests
Measure performance for large task graphs.

```csharp
[Benchmark]
public async Task ExecuteTaskWithManyDependencies()
{
    await _executor.ExecuteTaskAsync("root-with-50-deps");
}
```

#### 5. Add Mutation Testing
Use Stryker.NET to ensure test quality.

```bash
dotnet stryker
```

---

## Developer Experience

### High Priority

#### 1. Better `list` Command Output
Show more information about tasks.

```bash
$ rot list

Available tasks:
  build       Build the project          [shell]
  test        Run tests                  [shell]  depends: build
  deploy      Deploy to production       [shell]  depends: build, test

Groups: build (2), test (1), deploy (1)
```

#### 2. Add `describe` Command
Show detailed information about a specific task.

```bash
$ rot describe build

Task: build
  Label:   Build the project
  Type:    shell
  Command: dotnet build
  Cwd:     ./src
  Env:
    - DOTNET_CLI_TELEMETRY_OPTOUT=1
  Dependencies: clean, restore
  Dependents: test, deploy
```

#### 3. Add Shell Completions
Generate completions for bash, zsh, fish, PowerShell.

```bash
rot completion bash > /etc/bash_completion.d/rot
rot completion zsh > ~/.zsh/completions/_rot
```

#### 4. Add `--verbose` and `--quiet` Flags
Control output verbosity.

```bash
rot run build --verbose  # Show detailed execution info
rot run build --quiet    # Only show errors
rot run build -q         # Short form
```

### Medium Priority

#### 5. Colorized Task Graph Visualization
Show task dependency graph.

```bash
$ rot graph

build
├── clean
└── restore

test
└── build
    ├── clean
    └── restore

deploy
└── test
    └── build
        ├── clean
        └── restore
```

#### 6. Init Templates
Support different project types in `rot init`.

```bash
rot init --template dotnet
rot init --template node
rot init --template python
rot init --template docker
```

#### 7. Config File Auto-Discovery
Search up directory tree for tasks file.

```csharp
public static string? FindTasksFile(string startDir)
{
    var dir = startDir;
    while (dir != null)
    {
        var yamlPath = Path.Combine(dir, "tasks.yaml");
        var jsonPath = Path.Combine(dir, "tasks.json");

        if (File.Exists(yamlPath)) return yamlPath;
        if (File.Exists(jsonPath)) return jsonPath;

        dir = Path.GetDirectoryName(dir);
    }
    return null;
}
```

---

## Performance Optimizations

### Medium Priority

#### 1. Lazy Task Loading
Only parse tasks that are actually needed.

```csharp
private readonly Lazy<Dictionary<string, TaskDefinition>> _tasks;
```

#### 2. Task Execution Caching
Cache results of completed tasks within a session.

```csharp
private readonly Dictionary<string, int> _completedTasks = new();

public async Task<int> ExecuteTaskAsync(string taskName)
{
    if (_completedTasks.TryGetValue(taskName, out var cachedResult))
        return cachedResult;
    // ...
}
```

#### 3. Parallel Config Validation
Validate all tasks concurrently at load time.

```csharp
var validationTasks = _tasks.Select(t => Task.Run(() => Validate(t)));
var results = await Task.WhenAll(validationTasks);
```

#### 4. Efficient Argument Building
Use `StringBuilder` for large argument lists.

```csharp
private string BuildArguments(string[] args)
{
    if (args.Length == 0) return string.Empty;
    if (args.Length == 1) return args[0];

    var sb = new StringBuilder(args.Sum(a => a.Length + 1));
    foreach (var arg in args)
    {
        if (sb.Length > 0) sb.Append(' ');
        sb.Append(arg);
    }
    return sb.ToString();
}
```

---

## Security Enhancements

### High Priority

#### 1. Environment Variable Sanitization
Prevent shell injection via environment variables.

```csharp
public static bool IsValidEnvValue(string value)
{
    // Reject values with shell metacharacters in unsafe contexts
    var dangerous = new[] { ';', '|', '&', '$', '`', '\n', '\r' };
    return !dangerous.Any(c => value.Contains(c));
}
```

#### 2. Command Validation
Validate commands before execution.

```csharp
public static bool IsSafeCommand(string command)
{
    // Warn or block dangerous patterns
    var dangerous = new[] { "rm -rf /", ":(){ :|:& };:", "dd if=" };
    return !dangerous.Any(d => command.Contains(d, StringComparison.OrdinalIgnoreCase));
}
```

#### 3. Working Directory Validation
Ensure working directories are within expected paths.

```csharp
public static bool IsValidWorkingDirectory(string cwd, string projectRoot)
{
    var fullPath = Path.GetFullPath(cwd);
    return fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase);
}
```

### Medium Priority

#### 4. Secrets Management
Support secure environment variable sources.

```yaml
tasks:
  deploy:
    command: ./deploy.sh
    env:
      API_KEY: ${secret:api-key}  # Load from secure store
      DB_PASS: ${env:DB_PASSWORD}  # Load from parent env
```

#### 5. Audit Logging
Log task executions for compliance.

```csharp
public class AuditLogger
{
    public void LogExecution(string taskName, string command, DateTime timestamp, int exitCode)
    {
        // Write to audit log file
    }
}
```

---

## Implementation Priority Summary

### Phase 1 (Core Improvements)
1. Task timeout support
2. Dry run mode
3. Unit tests
4. Structured logging
5. Configuration validation

### Phase 2 (Enhanced Features)
1. Task groups and batch execution
2. Variable substitution
3. Watch mode
4. `--verbose`/`--quiet` flags
5. Better `list` output

### Phase 3 (Advanced Features)
1. Conditional execution
2. Task hooks
3. Profile support
4. Task caching
5. Plugin system

---

## Related Resources

- [VS Code Tasks Documentation](https://code.visualstudio.com/docs/editor/tasks)
- [Make](https://www.gnu.org/software/make/)
- [Just](https://github.com/casey/just)
- [Task](https://taskfile.dev/)
