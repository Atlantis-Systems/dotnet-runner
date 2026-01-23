namespace Rot.Models;

/// <summary>
/// Configuration for task output caching based on input file hashes.
/// </summary>
public class TaskCacheConfig
{
    /// <summary>
    /// Glob patterns for input files to hash. If these haven't changed, the cache is valid.
    /// </summary>
    public string[] Inputs { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Output directories/files that indicate a successful execution.
    /// </summary>
    public string[] Outputs { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Cache time-to-live in minutes. After this time, cache is invalidated.
    /// Default is null (no expiration).
    /// </summary>
    public int? TtlMinutes { get; set; }
}
