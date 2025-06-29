using System.CommandLine;
using DotRunner.Services;

var fileOption = new Option<string>(
    aliases: ["--file", "-f"],
    description: "Path to the tasks.json file",
    getDefaultValue: () => "tasks.json");

var concurrentOption = new Option<bool>(
    aliases: ["--concurrent", "-c"],
    description: "Enable concurrent execution of tasks",
    getDefaultValue: () => true);

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
    initCommand,
    fileOption,
    concurrentOption
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

rootCommand.SetHandler((string file, bool concurrent) =>
{
    // Show help when no arguments provided
    Console.WriteLine("Use 'dotnet-runner --help' for usage information.");
}, fileOption, concurrentOption);

// Pre-process arguments to handle direct task execution
if (args.Length > 0)
{
    var firstArg = args[0];
    
    // Skip if it's a known command or starts with -- (option)
    if (firstArg != "list" && firstArg != "run" && firstArg != "init" && !firstArg.StartsWith("--"))
    {
        try
        {
            // Try to load tasks and see if first argument matches a task name
            var file = "tasks.json";
            var concurrent = true;
            
            // Parse file and concurrent options from remaining args
            for (int i = 1; i < args.Length; i++)
            {
                if ((args[i] == "--file" || args[i] == "-f") && i + 1 < args.Length)
                {
                    file = args[i + 1];
                    i++; // Skip the value
                }
                else if (args[i] == "--concurrent" || args[i] == "-c")
                {
                    concurrent = true;
                }
            }
            
            var executor = TaskExecutor.LoadFromFile(file, concurrent);
            if (executor.HasTask(firstArg))
            {
                var result = await executor.ExecuteTaskAsync(firstArg);
                return result;
            }
        }
        catch
        {
            // If task loading fails, fall back to normal command parsing
        }
    }
}

return await rootCommand.InvokeAsync(args);