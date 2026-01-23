namespace Rot.Models;

public class TasksConfig
{
    public Dictionary<string, TaskDefinition> Tasks { get; set; } = new();
    public Dictionary<string, string> Variables { get; set; } = new();
}

