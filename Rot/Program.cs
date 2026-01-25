using System.CommandLine;
using System.CommandLine.Invocation;
using Rot.Logging;
using Rot.Models;
using Rot.Services;

var fileOption = new Option<string>(
    aliases: ["--file", "-f"],
    description: "Path to the tasks file (tasks.json or tasks.yaml). Auto-discovers up the directory tree if not specified.",
    getDefaultValue: () => TasksFileDiscovery.FindTasksFileOrDefault(null));

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

var outputOption = new Option<string?>(
    aliases: ["--output", "-o"],
    description: "Write task output to a file",
    getDefaultValue: () => null);

var jsonOption = new Option<bool>(
    aliases: ["--json"],
    description: "Output results as JSON",
    getDefaultValue: () => false);

// Phase 6: New options
var nonInteractiveOption = new Option<bool>(
    aliases: ["--non-interactive", "-y"],
    description: "Skip prompts and use default values",
    getDefaultValue: () => false);

var auditOption = new Option<bool>(
    aliases: ["--audit"],
    description: "Enable audit logging of all task executions",
    getDefaultValue: () => false);

var auditFileOption = new Option<string?>(
    aliases: ["--audit-file"],
    description: "Path to the audit log file",
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
    logFileOption,
    groupOption,
    patternOption,
    tagOption,
    profileOption,
    noCacheOption,
    outputOption,
    jsonOption,
    nonInteractiveOption,
    auditOption,
    auditFileOption
};

var initCommand = new Command("init", "Initialize a new tasks file");
var formatOption = new Option<string>(
    aliases: ["--format"],
    description: "File format (json or yaml)",
    getDefaultValue: () => "json");
var templateOption = new Option<string>(
    aliases: ["--template", "-t"],
    description: "Project template: dotnet, node, python, docker, or default",
    getDefaultValue: () => "default");
initCommand.AddOption(formatOption);
initCommand.AddOption(templateOption);

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

var completionCommand = new Command("completion", "Generate shell completion scripts");
var shellArgument = new Argument<string>("shell", "Shell type: bash, zsh, fish, or powershell");
completionCommand.Add(shellArgument);

var graphCommand = new Command("graph", "Display task dependency graph")
{
    fileOption
};
var graphTaskArgument = new Argument<string?>("task", () => null, "Show graph for a specific task (optional)");
graphCommand.Add(graphTaskArgument);

// Phase 6: Audit command
var auditCommand = new Command("audit", "View recent audit log entries");
var auditCountOption = new Option<int>(
    aliases: ["--count", "-n"],
    description: "Number of entries to show",
    getDefaultValue: () => 20);
auditCommand.AddOption(auditFileOption);
auditCommand.AddOption(auditCountOption);
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
    graphCommand,
    completionCommand,
    auditCommand,
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

runCommand.SetHandler(async (InvocationContext context) =>
{
    string file = context.ParseResult.GetValueForOption(fileOption)!;
    string? task = context.ParseResult.GetValueForArgument(taskArgument);
    bool concurrent = context.ParseResult.GetValueForOption(concurrentOption);
    bool dryRun = context.ParseResult.GetValueForOption(dryRunOption);
    bool verbose = context.ParseResult.GetValueForOption(verboseOption);
    bool quiet = context.ParseResult.GetValueForOption(quietOption);
    string? logFile = context.ParseResult.GetValueForOption(logFileOption);
    string? group = context.ParseResult.GetValueForOption(groupOption);
    string? pattern = context.ParseResult.GetValueForOption(patternOption);
    string? tag = context.ParseResult.GetValueForOption(tagOption);
    string? profile = context.ParseResult.GetValueForOption(profileOption);
    bool noCache = context.ParseResult.GetValueForOption(noCacheOption);
    string? outputFile = context.ParseResult.GetValueForOption(outputOption);
    bool jsonOutput = context.ParseResult.GetValueForOption(jsonOption);
    bool nonInteractive = context.ParseResult.GetValueForOption(nonInteractiveOption);
    bool enableAudit = context.ParseResult.GetValueForOption(auditOption);
    string? auditFile = context.ParseResult.GetValueForOption(auditFileOption);

    try
    {
        ITaskLogger logger = CreateLogger(verbose, quiet, logFile);
        bool captureOutput = !string.IsNullOrEmpty(outputFile) || jsonOutput;
        using TaskExecutor executor = TaskExecutor.LoadFromFile(file, concurrent, dryRun, noCache, profile, logger, captureOutput, nonInteractive, auditFile, enableAudit);
        TasksResult tasksResult;

        // Determine which tasks to run based on options
        if (!string.IsNullOrEmpty(group))
        {
            IEnumerable<string> tasks = executor.GetTasksByGroup(group);
            tasksResult = await executor.ExecuteTasksWithResultAsync(tasks);
        }
        else if (!string.IsNullOrEmpty(pattern))
        {
            IEnumerable<string> tasks = executor.GetTasksByPattern(pattern);
            tasksResult = await executor.ExecuteTasksWithResultAsync(tasks);
        }
        else if (!string.IsNullOrEmpty(tag))
        {
            IEnumerable<string> tasks = executor.GetTasksByTag(tag);
            tasksResult = await executor.ExecuteTasksWithResultAsync(tasks);
        }
        else if (!string.IsNullOrEmpty(task))
        {
            // Check if it's an alias first
            if (executor.HasAlias(task))
            {
                tasksResult = await executor.ExecuteAliasWithResultAsync(task);
            }
            else
            {
                var result = await executor.ExecuteTaskWithResultAsync(task);
                tasksResult = new TasksResult { Results = new[] { result } };
            }
        }
        else
        {
            Console.Error.WriteLine("Error: Please specify a task name, --group, --pattern, or --tag");
            Environment.Exit(1);
            return;
        }

        // Handle output
        if (jsonOutput)
        {
            var jsonResult = System.Text.Json.JsonSerializer.Serialize(tasksResult, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
            Console.WriteLine(jsonResult);

            if (!string.IsNullOrEmpty(outputFile))
            {
                await File.WriteAllTextAsync(outputFile, jsonResult);
            }
        }
        else if (!string.IsNullOrEmpty(outputFile))
        {
            // Write captured output to file
            var output = new System.Text.StringBuilder();
            foreach (var result in tasksResult.Results)
            {
                if (!string.IsNullOrEmpty(result.CombinedOutput))
                {
                    output.AppendLine($"=== Task: {result.TaskName} ===");
                    output.AppendLine(result.CombinedOutput);
                    output.AppendLine();
                }
            }
            await File.WriteAllTextAsync(outputFile, output.ToString());
            Console.WriteLine($"Output written to: {outputFile}");
        }

        (logger as IDisposable)?.Dispose();
        Environment.Exit(tasksResult.ExitCode);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
});

initCommand.SetHandler((string format, string template) =>
{
    try
    {
        // Validate template
        if (!InitTemplates.AvailableTemplates.Contains(template.ToLowerInvariant()))
        {
            Console.Error.WriteLine($"Unknown template '{template}'. Available templates: {string.Join(", ", InitTemplates.AvailableTemplates)}");
            Environment.Exit(1);
            return;
        }

        string fileName;
        string content;

        if (format.Equals("yaml", StringComparison.InvariantCultureIgnoreCase) || format.Equals("yml", StringComparison.InvariantCultureIgnoreCase))
        {
            fileName = "tasks.yaml";
            content = InitTemplates.GetTemplateYaml(template);
        }
        else
        {
            fileName = "tasks.json";
            content = InitTemplates.GetTemplateJson(template);
        }

        if (File.Exists(fileName))
        {
            Console.WriteLine($"{fileName} already exists in the current directory.");
            Environment.Exit(1);
        }

        File.WriteAllText(fileName, content);
        Console.WriteLine($"Created {fileName} with {template} template.");
        Console.WriteLine();
        Console.WriteLine("Available templates:");
        foreach (var t in InitTemplates.AvailableTemplates)
        {
            var marker = t == template.ToLowerInvariant() ? " (current)" : "";
            Console.WriteLine($"  - {t}{marker}");
        }
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error creating tasks file: {ex.Message}");
        Environment.Exit(1);
    }
}, formatOption, templateOption);

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

completionCommand.SetHandler((string shell) =>
{
    try
    {
        string completion = ShellCompletionGenerator.Generate(shell);
        Console.WriteLine(completion);
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
}, shellArgument);

graphCommand.SetHandler((string file, string? task) =>
{
    try
    {
        TaskExecutor executor = TaskExecutor.LoadFromFile(file);
        executor.PrintGraph(task);
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
}, fileOption, graphTaskArgument);

auditCommand.SetHandler((string? auditFile, int count) =>
{
    try
    {
        var logger = new ConsoleLogger(LogLevel.Info);
        var auditLogger = new AuditLogger(auditFile, logger, true);
        var entries = auditLogger.GetRecentEntries(count);

        if (entries.Count == 0)
        {
            Console.WriteLine("No audit entries found.");
            Environment.Exit(0);
            return;
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Recent Audit Entries ({entries.Count})");
        Console.ResetColor();
        Console.WriteLine(new string('═', 80));
        Console.WriteLine();

        foreach (var entry in entries)
        {
            var statusColor = entry.Success ? ConsoleColor.Green : ConsoleColor.Red;
            var statusIcon = entry.Success ? "✓" : "✗";

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] ");
            Console.ResetColor();

            Console.ForegroundColor = statusColor;
            Console.Write($"{statusIcon} ");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"{entry.TaskName,-20} ");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write($"{entry.EventType,-15} ");
            Console.ResetColor();

            if (entry.DurationMs.HasValue)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"({entry.DurationMs}ms) ");
                Console.ResetColor();
            }

            if (!string.IsNullOrEmpty(entry.ErrorMessage))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"- {entry.ErrorMessage}");
                Console.ResetColor();
            }

            Console.WriteLine();
        }

        Console.WriteLine();
        auditLogger.Dispose();
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
}, auditFileOption, auditCountOption);

watchCommand.SetHandler(async (string file, string task, string glob, int debounce, bool concurrent, bool verbose, bool quiet, string? logFile) =>
{
    try
    {
        ITaskLogger logger = CreateLogger(verbose, quiet, logFile);
        TaskExecutor executor = TaskExecutor.LoadFromFile(file, concurrent, false, false, null, logger);

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
            string[] parts = glob.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1 && !parts[0].Contains('*'))
            {
                watchPath = Path.Combine(watchPath, parts[0]);
            }
        }

        if (glob.Contains('.'))
        {
            int extIndex = glob.LastIndexOf('.');
            extension = glob.Substring(extIndex);
            if (extension.Contains('*'))
            {
                extension = "*.*";
            }
        }

        using FileSystemWatcher watcher = new(watchPath);
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
        watcher.Renamed += OnChange;

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
    var knownCommands = new[] { "list", "run", "init", "describe", "watch", "graph", "completion" };

    // Skip if it's a known command or starts with -- (option)
    if (!knownCommands.Contains(firstArg) && !firstArg.StartsWith("--") && !firstArg.StartsWith("-"))
    {
        try
        {
            // Try to load tasks and see if first argument matches a task name
            string? file = null;

            // Parse file option from remaining args
            for (int i = 1; i < args.Length; i++)
            {
                if ((args[i] == "--file" || args[i] == "-f") && i + 1 < args.Length)
                {
                    file = args[i + 1];
                    break;
                }
            }

            // Use auto-discovery if not specified
            file = TasksFileDiscovery.FindTasksFileOrDefault(file);

            var executor = TaskExecutor.LoadFromFile(file);
            if (executor.HasTaskOrAlias(firstArg))
            {
                // Insert "run" command to handle task/alias execution through normal flow
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