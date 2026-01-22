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
    logFileOption
};

var initCommand = new Command("init", "Initialize a new tasks file");
var formatOption = new Option<string>(
    aliases: ["--format"],
    description: "File format (json or yaml)",
    getDefaultValue: () => "json");
initCommand.AddOption(formatOption);

var taskArgument = new Argument<string>("task", "Name of the task to run");
runCommand.Add(taskArgument);

var rootCommand = new RootCommand("A .NET tool for running tasks defined in tasks.json or tasks.yaml files")
{
    listCommand,
    runCommand,
    initCommand,
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

runCommand.SetHandler(async (string file, string task, bool concurrent, bool dryRun, bool verbose, bool quiet, string? logFile) =>
{
    try
    {
        ITaskLogger logger = CreateLogger(verbose, quiet, logFile);
        var executor = TaskExecutor.LoadFromFile(file, concurrent, dryRun, logger);
        var result = await executor.ExecuteTaskAsync(task);
        (logger as IDisposable)?.Dispose();
        Environment.Exit(result);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
}, fileOption, taskArgument, concurrentOption, dryRunOption, verboseOption, quietOption, logFileOption);

initCommand.SetHandler((string format) =>
{
    try
    {
        var defaultTasksJson = """
        {
          "version": "2.0.0",
          "tasks": {
            "build": {
              "label": "Build the project",
              "type": "shell",
              "command": "dotnet build",
              "group": "build",
              "presentation": {
                "echo": true,
                "reveal": true,
                "focus": false,
                "panel": "shared"
              }
            },
            "test": {
              "label": "Run tests",
              "type": "shell", 
              "command": "dotnet test",
              "group": "test",
              "dependsOn": ["build"],
              "presentation": {
                "echo": true,
                "reveal": true
              }
            },
            "clean": {
              "label": "Clean build artifacts",
              "type": "shell",
              "command": "dotnet clean",
              "group": "build"
            },
            "restore": {
              "label": "Restore packages",
              "type": "shell",
              "command": "dotnet restore"
            }
          }
        }
        """;

        var defaultTasksYaml = """
        version: "2.0.0"
        tasks:
          build:
            label: Build the project
            type: shell
            command: dotnet build
            group: build
            presentation:
              echo: true
              reveal: true
              focus: false
              panel: shared
          test:
            label: Run tests
            type: shell
            command: dotnet test
            group: test
            dependsOn:
              - build
            presentation:
              echo: true
              reveal: true
          clean:
            label: Clean build artifacts
            type: shell
            command: dotnet clean
            group: build
          restore:
            label: Restore packages
            type: shell
            command: dotnet restore
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

rootCommand.SetHandler((string file, bool concurrent, bool dryRun) =>
{
    // Show help when no arguments provided
    Console.WriteLine("Use 'rot --help' for usage information.");
}, fileOption, concurrentOption, dryRunOption);

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

    // Skip if it's a known command or starts with -- (option)
    if (firstArg != "list" && firstArg != "run" && firstArg != "init" && !firstArg.StartsWith("--") && !firstArg.StartsWith("-"))
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