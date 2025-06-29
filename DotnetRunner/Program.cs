using System.CommandLine;
using DotnetRunner.Services;

var fileOption = new Option<string>(
    aliases: ["--file", "-f"],
    description: "Path to the tasks.json file",
    getDefaultValue: () => "tasks.json");

var concurrentOption = new Option<bool>(
    aliases: ["--concurrent", "-c"],
    description: "Enable concurrent execution of tasks",
    getDefaultValue: () => false);

var listCommand = new Command("list", "List all available tasks")
{
    fileOption
};

var runCommand = new Command("run", "Run a specific task")
{
    fileOption,
    concurrentOption
};

var initCommand = new Command("init", "Initialize a new tasks.json file");

var taskArgument = new Argument<string>("task", "Name of the task to run");
runCommand.Add(taskArgument);

var rootCommand = new RootCommand("A .NET tool for running tasks defined in tasks.json files")
{
    listCommand,
    runCommand,
    initCommand
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

runCommand.SetHandler(async (string file, string task, bool concurrent) =>
{
    try
    {
        var executor = TaskExecutor.LoadFromFile(file, concurrent);
        var result = await executor.ExecuteTaskAsync(task);
        Environment.Exit(result);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
}, fileOption, taskArgument, concurrentOption);

initCommand.SetHandler(() =>
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

        if (File.Exists("tasks.json"))
        {
            Console.WriteLine("tasks.json already exists in the current directory.");
            Environment.Exit(1);
        }

        File.WriteAllText("tasks.json", defaultTasksJson);
        Console.WriteLine("Created tasks.json with default .NET tasks.");
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error creating tasks.json: {ex.Message}");
        Environment.Exit(1);
    }
});

rootCommand.SetHandler(() =>
{
    Console.WriteLine("Use 'dotnet-runner --help' for usage information.");
});

return await rootCommand.InvokeAsync(args);
