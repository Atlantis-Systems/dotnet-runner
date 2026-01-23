using System.CommandLine;
using Rot.Logging;
using Rot.Services;

var fileOption = new Option<string>(
    aliases: ["--file", "-f"],
    description: "Path to the tasks file (tasks.json or tasks.yaml)",
    getDefaultValue: () => File.Exists("tasks.yaml") ? "tasks.yaml" : "tasks.json");

var concurrentOption = new Option<bool>(
    aliases: ["--concurrent", "-c"],
    description: "Enable concurrent execution of tasks",
    getDefaultValue: () => true);

var dryRunOption = new Option<bool>(
    aliases: ["--dry-run", "-n"],
    description: "Preview what would be executed without running commands",
    getDefaultValue: () => false);

var verboseOption = new Option<bool>(
    aliases: ["--verbose", "-v"],
    description: "Show detailed execution information",
    getDefaultValue: () => false);

var quietOption = new Option<bool>(
    aliases: ["--quiet", "-q"],
    description: "Only show errors",
    getDefaultValue: () => false);

var logFileOption = new Option<string?>(
    aliases: ["--log-file"],
    description: "Write logs to a file",
    getDefaultValue: () => null);

var groupOption = new Option<string?>(
    aliases: ["--group", "-g"],
    description: "Run all tasks in the specified group",
    getDefaultValue: () => null);

var patternOption = new Option<string?>(
    aliases: ["--pattern", "-p"],
    description: "Run all tasks matching the pattern (supports * and ? wildcards)",
    getDefaultValue: () => null);

var tagOption = new Option<string?>(
    aliases: ["--tag", "-t"],
    description: "Run all tasks with the specified tag",
    getDefaultValue: () => null);

var profileOption = new Option<string?>(
    aliases: ["--profile"],
    description: "Apply a profile configuration (variables and environment)",
    getDefaultValue: () => null);

var noCacheOption = new Option<bool>(
    aliases: ["--no-cache"],
    description: "Disable task caching",
    getDefaultValue: () => false);

var listCommand = new Command("list", "List all available tasks")
{
    fileOption
};

var runCommand = new Command("run", "Run a specific task")
{
    fileOption,
    concurrentOption,
    dryRunOption,
    verboseOption,
    quietOption,
    logFileOption,
    groupOption,
    patternOption,
    tagOption,
    profileOption,
    noCacheOption
};

var initCommand = new Command("init", "Initialize a new tasks file");
var formatOption = new Option<string>(
    aliases: ["--format"],
    description: "File format (json or yaml)",
    getDefaultValue: () => "json");
initCommand.AddOption(formatOption);

var describeCommand = new Command("describe", "Show detailed information about a task")
{
    fileOption
};
var describeTaskArgument = new Argument<string>("task", "Name of the task to describe");
describeCommand.Add(describeTaskArgument);

var watchCommand = new Command("watch", "Watch for file changes and re-run a task")
{
    fileOption,
    concurrentOption,
    verboseOption,
    quietOption,
    logFileOption
};
var watchTaskArgument = new Argument<string>("task", "Name of the task to run on changes");
var globOption = new Option<string>(
    aliases: ["--glob"],
    description: "Glob pattern for files to watch (e.g., 'src/**/*.cs')",
    getDefaultValue: () => "**/*");
var debounceOption = new Option<int>(
    aliases: ["--debounce"],
    description: "Debounce time in milliseconds",
    getDefaultValue: () => 500);
watchCommand.Add(watchTaskArgument);
watchCommand.AddOption(globOption);
watchCommand.AddOption(debounceOption);

var taskArgument = new Argument<string?>("task", () => null, "Name of the task to run");
runCommand.Add(taskArgument);

var rootCommand = new RootCommand("A .NET tool for running tasks defined in tasks.json or tasks.yaml files")
{
    listCommand,
    runCommand,
    initCommand,
    describeCommand,
    watchCommand,
    fileOption,
    concurrentOption,
    dryRunOption
};

listCommand.SetHandler((string file) =>
{
    try
    {
        var executor = TaskExecutor.LoadFromFile(file);
        executor.ListTasks();
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
}, fileOption);

runCommand.SetHandler(async (string file, string? task, bool concurrent, bool dryRun, bool verbose, bool quiet, string? logFile, string? group, string? pattern, string? tag, string? profile, bool noCache) =>
{
    try
    {
        ITaskLogger logger = CreateLogger(verbose, quiet, logFile);
        var executor = TaskExecutor.LoadFromFile(file, concurrent, dryRun, noCache, profile, logger);
        int result;

        // Determine which tasks to run based on options
        if (!string.IsNullOrEmpty(group))
        {
            var tasks = executor.GetTasksByGroup(group);
            result = await executor.ExecuteTasksAsync(tasks);
        }
        else if (!string.IsNullOrEmpty(pattern))
        {
            var tasks = executor.GetTasksByPattern(pattern);
            result = await executor.ExecuteTasksAsync(tasks);
        }
        else if (!string.IsNullOrEmpty(tag))
        {
            var tasks = executor.GetTasksByTag(tag);
            result = await executor.ExecuteTasksAsync(tasks);
        }
        else if (!string.IsNullOrEmpty(task))
        {
            result = await executor.ExecuteTaskAsync(task);
        }
        else
        {
            Console.Error.WriteLine("Error: Please specify a task name, --group, --pattern, or --tag");
            Environment.Exit(1);
            return;
        }

        (logger as IDisposable)?.Dispose();
        Environment.Exit(result);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
}, fileOption, taskArgument, concurrentOption, dryRunOption, verboseOption, quietOption, logFileOption, groupOption, patternOption, tagOption, profileOption, noCacheOption);

initCommand.SetHandler((string format) =>
{
    try
    {
        var defaultTasksJson = """
        {
          "version": "3.0.0",
          "variables": {
            "config": "Debug",
            "outputDir": "./bin"
          },
          "profiles": {
            "dev": {
              "variables": { "config": "Debug" },
              "env": { "DOTNET_ENVIRONMENT": "Development" }
            },
            "prod": {
              "variables": { "config": "Release" },
              "env": { "DOTNET_ENVIRONMENT": "Production" }
            }
          },
          "tasks": {
            "build": {
              "label": "Build the project",
              "type": "shell",
              "command": "dotnet build -c ${config}",
              "group": "build",
              "tags": ["ci", "dev"],
              "cache": {
                "inputs": ["**/*.cs", "**/*.csproj"],
                "outputs": ["bin/", "obj/"]
              }
            },
            "test": {
              "label": "Run tests",
              "type": "shell",
              "command": "dotnet test -c ${config}",
              "group": "test",
              "tags": ["ci", "dev"],
              "dependsOn": ["build"]
            },
            "clean": {
              "label": "Clean build artifacts",
              "type": "shell",
              "command": "dotnet clean",
              "group": "build",
              "tags": ["dev"]
            },
            "restore": {
              "label": "Restore packages",
              "type": "shell",
              "command": "dotnet restore",
              "tags": ["ci"]
            },
            "deploy": {
              "label": "Deploy application",
              "type": "shell",
              "command": "echo Deploying...",
              "condition": {
                "env": { "CI": "true" }
              },
              "preTasks": ["build", "test"]
            }
          }
        }
        """;

        var defaultTasksYaml = """
        version: "3.0.0"
        variables:
          config: Debug
          outputDir: ./bin
        profiles:
          dev:
            variables:
              config: Debug
            env:
              DOTNET_ENVIRONMENT: Development
          prod:
            variables:
              config: Release
            env:
              DOTNET_ENVIRONMENT: Production
        tasks:
          build:
            label: Build the project
            type: shell
            command: dotnet build -c ${config}
            group: build
            tags:
              - ci
              - dev
            cache:
              inputs:
                - "**/*.cs"
                - "**/*.csproj"
              outputs:
                - bin/
                - obj/
          test:
            label: Run tests
            type: shell
            command: dotnet test -c ${config}
            group: test
            tags:
              - ci
              - dev
            dependsOn:
              - build
          clean:
            label: Clean build artifacts
            type: shell
            command: dotnet clean
            group: build
            tags:
              - dev
          restore:
            label: Restore packages
            type: shell
            command: dotnet restore
            tags:
              - ci
          deploy:
            label: Deploy application
            type: shell
            command: echo Deploying...
            condition:
              env:
                CI: "true"
            preTasks:
              - build
              - test
        """;

        string fileName;
        string content;
        
        if (format.ToLowerInvariant() == "yaml" || format.ToLowerInvariant() == "yml")
        {
            fileName = "tasks.yaml";
            content = defaultTasksYaml;
        }
        else
        {
            fileName = "tasks.json";
            content = defaultTasksJson;
        }

        if (File.Exists(fileName))
        {
            Console.WriteLine($"{fileName} already exists in the current directory.");
            Environment.Exit(1);
        }

        File.WriteAllText(fileName, content);
        Console.WriteLine($"Created {fileName} with default .NET tasks.");
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error creating tasks file: {ex.Message}");
        Environment.Exit(1);
    }
}, formatOption);

describeCommand.SetHandler((string file, string task) =>
{
    try
    {
        var executor = TaskExecutor.LoadFromFile(file);
        executor.DescribeTask(task);
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
}, fileOption, describeTaskArgument);

watchCommand.SetHandler(async (string file, string task, string glob, int debounce, bool concurrent, bool verbose, bool quiet, string? logFile) =>
{
    try
    {
        ITaskLogger logger = CreateLogger(verbose, quiet, logFile);
        var executor = TaskExecutor.LoadFromFile(file, concurrent, false, false, null, logger);

        if (!executor.HasTask(task))
        {
            Console.Error.WriteLine($"Error: Task '{task}' not found");
            Environment.Exit(1);
            return;
        }

        Console.WriteLine($"Watching for changes (pattern: {glob}, debounce: {debounce}ms)");
        Console.WriteLine($"Press Ctrl+C to stop");
        Console.WriteLine();

        // Convert glob to watch path and filter
        var watchPath = Directory.GetCurrentDirectory();
        var extension = "*.*";

        // Extract path and extension from glob if possible
        if (glob.Contains('/') || glob.Contains('\\'))
        {
            var parts = glob.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1 && !parts[0].Contains('*'))
            {
                watchPath = Path.Combine(watchPath, parts[0]);
            }
        }

        if (glob.Contains('.'))
        {
            var extIndex = glob.LastIndexOf('.');
            extension = glob.Substring(extIndex);
            if (extension.Contains('*'))
            {
                extension = "*.*";
            }
        }

        using var watcher = new FileSystemWatcher(watchPath);
        watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
        watcher.IncludeSubdirectories = glob.Contains("**");
        watcher.Filter = extension;

        var lastExecution = DateTime.MinValue;
        var executionLock = new object();

        async void OnChange(object sender, FileSystemEventArgs e)
        {
            // Simple glob matching
            if (!MatchesGlob(e.FullPath, glob))
                return;

            lock (executionLock)
            {
                var now = DateTime.Now;
                if ((now - lastExecution).TotalMilliseconds < debounce)
                    return;
                lastExecution = now;
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Change detected: {e.Name}");
            Console.ResetColor();
            Console.WriteLine();

            // Reload executor to pick up any config changes
            var freshExecutor = TaskExecutor.LoadFromFile(file, concurrent, false, false, null, logger);
            await freshExecutor.ExecuteTaskAsync(task);
        }

        watcher.Changed += OnChange;
        watcher.Created += OnChange;
        watcher.Renamed += (s, e) => OnChange(s, e);

        watcher.EnableRaisingEvents = true;

        // Run the task once initially
        Console.WriteLine("Running initial task execution...");
        await executor.ExecuteTaskAsync(task);
        Console.WriteLine();
        Console.WriteLine("Watching for changes...");

        // Wait indefinitely
        await Task.Delay(Timeout.Infinite);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
}, fileOption, watchTaskArgument, globOption, debounceOption, concurrentOption, verboseOption, quietOption, logFileOption);

rootCommand.SetHandler((string file, bool concurrent, bool dryRun) =>
{
    // Show help when no arguments provided
    Console.WriteLine("Use 'rot --help' for usage information.");
}, fileOption, concurrentOption, dryRunOption);

bool MatchesGlob(string path, string pattern)
{
    // Simple glob matching - convert to regex
    var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
        .Replace("\\*\\*", ".*")
        .Replace("\\*", "[^/\\\\]*")
        .Replace("\\?", ".") + "$";

    var normalizedPath = path.Replace('\\', '/');
    return System.Text.RegularExpressions.Regex.IsMatch(normalizedPath, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}

ITaskLogger CreateLogger(bool verbose, bool quiet, string? logFile)
{
    LogLevel level;
    if (quiet)
        level = LogLevel.Error;
    else if (verbose)
        level = LogLevel.Debug;
    else
        level = LogLevel.Info;

    if (!string.IsNullOrEmpty(logFile))
    {
        return new FileLogger(logFile, level);
    }

    return new ConsoleLogger(level);
}

// Pre-process arguments to handle direct task execution
if (args.Length > 0)
{
    var firstArg = args[0];
    var knownCommands = new[] { "list", "run", "init", "describe", "watch" };

    // Skip if it's a known command or starts with -- (option)
    if (!knownCommands.Contains(firstArg) && !firstArg.StartsWith("--") && !firstArg.StartsWith("-"))
    {
        try
        {
            // Try to load tasks and see if first argument matches a task name
            var file = File.Exists("tasks.yaml") ? "tasks.yaml" : "tasks.json";

            // Parse file option from remaining args
            for (int i = 1; i < args.Length; i++)
            {
                if ((args[i] == "--file" || args[i] == "-f") && i + 1 < args.Length)
                {
                    file = args[i + 1];
                    break;
                }
            }

            var executor = TaskExecutor.LoadFromFile(file);
            if (executor.HasTask(firstArg))
            {
                // Insert "run" command to handle task execution through normal flow
                var newArgs = new List<string> { "run", firstArg };
                newArgs.AddRange(args.Skip(1));
                args = newArgs.ToArray();
            }
        }
        catch
        {
            // If task loading fails, fall back to normal command parsing
        }
    }
}

return await rootCommand.InvokeAsync(args);