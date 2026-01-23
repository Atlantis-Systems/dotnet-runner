using Rot.Logging;
using Rot.Models;

namespace Rot.Services;

/// <summary>
/// Interface for plugin-provided task type executors.
/// </summary>
public interface ITaskTypeProvider
{
    /// <summary>
    /// The task type this provider handles (e.g., "docker", "kubernetes").
    /// </summary>
    string TaskType { get; }

    /// <summary>
    /// Executes a task of this type.
    /// </summary>
    /// <param name="task">The task definition.</param>
    /// <param name="taskName">The task name.</param>
    /// <param name="logger">Logger for output.</param>
    /// <returns>Exit code (0 for success).</returns>
    Task<int> ExecuteAsync(TaskDefinition task, string taskName, ITaskLogger logger);
}
