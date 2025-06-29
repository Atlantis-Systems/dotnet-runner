namespace DotRunner.Models;

public class TasksConfig
{
    public string Version { get; set; } = "2.0.0";
    public Dictionary<string, TaskDefinition> Tasks { get; set; } = new();
}

public class TaskDefinition
{
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "shell";
    public string Command { get; set; } = string.Empty;
    public string[] Args { get; set; } = Array.Empty<string>();
    public TaskOptions Options { get; set; } = new();
    public string Group { get; set; } = string.Empty;
    public PresentationOptions Presentation { get; set; } = new();
    public string[] DependsOn { get; set; } = Array.Empty<string>();
    public string RunOptions { get; set; } = string.Empty;
    public bool AllowConcurrent { get; set; } = false;
}

public class TaskOptions
{
    public string Cwd { get; set; } = string.Empty;
    public Dictionary<string, string> Env { get; set; } = new();
    public string Shell { get; set; } = string.Empty;
}

public class PresentationOptions
{
    public bool Echo { get; set; } = true;
    public bool Reveal { get; set; } = true;
    public bool Focus { get; set; } = false;
    public string Panel { get; set; } = "shared";
    public bool ShowReuseMessage { get; set; } = true;
    public bool Clear { get; set; } = false;
}