using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rot.Logging;
using Rot.Models;

namespace Rot.Services;

/// <summary>
/// Manages task result caching based on input file hashes.
/// </summary>
public class CacheManager
{
    private readonly string _cacheDir;
    private readonly ITaskLogger _logger;
    private readonly string _workingDirectory;

    public CacheManager(ITaskLogger logger, string? workingDirectory = null, string? cacheDir = null)
    {
        _logger = logger;
        _workingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
        _cacheDir = cacheDir ?? Path.Combine(_workingDirectory, ".rot", "cache");
    }

    /// <summary>
    /// Checks if the cache is valid for a task.
    /// </summary>
    /// <param name="taskName">The task name.</param>
    /// <param name="cache">The cache configuration.</param>
    /// <returns>True if cache is valid and task can be skipped.</returns>
    public bool IsCacheValid(string taskName, TaskCacheConfig cache)
    {
        try
        {
            var cacheFile = GetCacheFilePath(taskName);
            if (!File.Exists(cacheFile))
            {
                _logger.Debug("Task '{TaskName}' cache miss: No cache file found", taskName);
                return false;
            }

            var cacheData = JsonSerializer.Deserialize<CacheData>(File.ReadAllText(cacheFile));
            if (cacheData == null)
            {
                _logger.Debug("Task '{TaskName}' cache miss: Invalid cache data", taskName);
                return false;
            }

            // Check TTL
            if (cache.TtlMinutes.HasValue)
            {
                var age = DateTime.UtcNow - cacheData.Timestamp;
                if (age.TotalMinutes > cache.TtlMinutes.Value)
                {
                    _logger.Debug("Task '{TaskName}' cache miss: Cache expired ({Age} min > {Ttl} min)", taskName, (int)age.TotalMinutes, cache.TtlMinutes.Value);
                    return false;
                }
            }

            // Check if inputs hash matches
            var currentHash = ComputeInputsHash(cache.Inputs);
            if (currentHash != cacheData.InputsHash)
            {
                _logger.Debug("Task '{TaskName}' cache miss: Inputs changed", taskName);
                return false;
            }

            // Check if all outputs exist
            if (cache.Outputs.Length > 0)
            {
                foreach (var output in cache.Outputs)
                {
                    var outputPath = GetFullPath(output);
                    if (!File.Exists(outputPath) && !Directory.Exists(outputPath))
                    {
                        _logger.Debug("Task '{TaskName}' cache miss: Output missing '{Output}'", taskName, output);
                        return false;
                    }
                }
            }

            _logger.Info("Task '{TaskName}' cache hit: Skipping execution", taskName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Warning("Task '{TaskName}' cache check failed: {Error}", taskName, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Saves cache data after successful task execution.
    /// </summary>
    /// <param name="taskName">The task name.</param>
    /// <param name="cache">The cache configuration.</param>
    public void SaveCache(string taskName, TaskCacheConfig cache)
    {
        try
        {
            var cacheDir = Path.GetDirectoryName(GetCacheFilePath(taskName));
            if (!string.IsNullOrEmpty(cacheDir) && !Directory.Exists(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
            }

            var cacheData = new CacheData
            {
                TaskName = taskName,
                InputsHash = ComputeInputsHash(cache.Inputs),
                Timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(cacheData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetCacheFilePath(taskName), json);

            _logger.Debug("Task '{TaskName}' cache saved", taskName);
        }
        catch (Exception ex)
        {
            _logger.Warning("Task '{TaskName}' cache save failed: {Error}", taskName, ex.Message);
        }
    }

    /// <summary>
    /// Invalidates cache for a specific task.
    /// </summary>
    /// <param name="taskName">The task name.</param>
    public void InvalidateCache(string taskName)
    {
        try
        {
            var cacheFile = GetCacheFilePath(taskName);
            if (File.Exists(cacheFile))
            {
                File.Delete(cacheFile);
                _logger.Debug("Task '{TaskName}' cache invalidated", taskName);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning("Task '{TaskName}' cache invalidation failed: {Error}", taskName, ex.Message);
        }
    }

    /// <summary>
    /// Clears all cache data.
    /// </summary>
    public void ClearAllCache()
    {
        try
        {
            if (Directory.Exists(_cacheDir))
            {
                Directory.Delete(_cacheDir, recursive: true);
                _logger.Info("All cache cleared");
            }
        }
        catch (Exception ex)
        {
            _logger.Warning("Cache clear failed: {Error}", ex.Message);
        }
    }

    private string GetCacheFilePath(string taskName)
    {
        // Sanitize task name for use as filename
        var safeTaskName = string.Join("_", taskName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_cacheDir, $"{safeTaskName}.json");
    }

    private string ComputeInputsHash(string[] inputPatterns)
    {
        using var sha256 = SHA256.Create();
        var sb = new StringBuilder();

        foreach (var pattern in inputPatterns)
        {
            var files = GetMatchingFiles(pattern);
            foreach (var file in files.OrderBy(f => f))
            {
                // Include file path and last write time in hash
                var fileInfo = new FileInfo(file);
                sb.Append(file);
                sb.Append('|');
                sb.Append(fileInfo.LastWriteTimeUtc.Ticks);
                sb.Append('|');
                sb.Append(fileInfo.Length);
                sb.Append('\n');
            }
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    private IEnumerable<string> GetMatchingFiles(string pattern)
    {
        try
        {
            var searchPath = _workingDirectory;
            var searchPattern = pattern;

            // Handle patterns with directory prefixes
            var lastSeparator = pattern.LastIndexOfAny(new[] { '/', '\\' });
            if (lastSeparator > 0 && !pattern.Substring(0, lastSeparator).Contains('*'))
            {
                searchPath = Path.Combine(_workingDirectory, pattern.Substring(0, lastSeparator));
                searchPattern = pattern.Substring(lastSeparator + 1);
            }

            if (!Directory.Exists(searchPath))
            {
                return Enumerable.Empty<string>();
            }

            var searchOption = pattern.Contains("**") ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            // Normalize the search pattern for Directory.GetFiles
            searchPattern = searchPattern.Replace("**/", "").Replace("**\\", "");
            if (string.IsNullOrEmpty(searchPattern))
            {
                searchPattern = "*";
            }

            return Directory.GetFiles(searchPath, searchPattern, searchOption);
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    private string GetFullPath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.Combine(_workingDirectory, path);
    }

    private class CacheData
    {
        public string TaskName { get; set; } = string.Empty;
        public string InputsHash { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
