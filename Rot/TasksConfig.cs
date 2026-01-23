namespace Rot.Models;

public class TasksConfig
{
    public Dictionary<string, TaskDefinition> Tasks { get; set; } = new();
    public Dictionary<string, string> Variables { get; set; } = new();

    // Phase 3: Profile support
    public Dictionary<string, TaskProfile> Profiles { get; set; } = new();

    // Phase 4: Task aliases
    public Dictionary<string, string[]> Aliases { get; set; } = new();
}

