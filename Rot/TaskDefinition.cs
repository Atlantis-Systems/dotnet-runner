namespace Rot.Models;

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
    public int? Timeout { get; set; }
    public string Group { get; set; } = string.Empty;
    public string[] Tags { get; set; } = Array.Empty<string>();

    // Phase 3: Conditional execution
    public TaskCondition? Condition { get; set; }

    // Phase 3: Task hooks
    public string[] PreTasks { get; set; } = Array.Empty<string>();
    public string[] PostTasks { get; set; } = Array.Empty<string>();

    // Phase 3: Task caching
    public TaskCacheConfig? Cache { get; set; }
}

