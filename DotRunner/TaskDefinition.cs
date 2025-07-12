namespace DotRunner.Models;

public class TaskDefinition
{
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "shell";
    public string Command { get; set; } = string.Empty;
    public string[] Args { get; set; } = Array.Empty<string>();
    public string Cwd { get; set; } = string.Empty;
    public Dictionary<string, string> Env { get; set; } = new();
    public string Shell { get; set; } = string.Empty;
    public bool Echo { get; set; } = true;
    public string[] DependsOn { get; set; } = Array.Empty<string>();
    public bool AllowConcurrent { get; set; } = false;
}

