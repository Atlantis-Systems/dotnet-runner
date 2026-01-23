using System.Reflection;
using Rot.Logging;

namespace Rot.Services;

/// <summary>
/// Loads and manages task type plugins.
/// </summary>
public class PluginLoader
{
    private readonly ITaskLogger _logger;
    private readonly string _pluginDirectory;
    private readonly Dictionary<string, ITaskTypeProvider> _providers = new(StringComparer.OrdinalIgnoreCase);

    public PluginLoader(ITaskLogger logger, string? pluginDirectory = null)
    {
        _logger = logger;
        _pluginDirectory = pluginDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".rot",
            "plugins");
    }

    /// <summary>
    /// Gets all loaded task type providers.
    /// </summary>
    public IReadOnlyDictionary<string, ITaskTypeProvider> Providers => _providers;

    /// <summary>
    /// Loads plugins from the specified plugin names.
    /// </summary>
    /// <param name="pluginNames">Names of plugins to load (e.g., "rot-plugin-docker").</param>
    public void LoadPlugins(IEnumerable<string> pluginNames)
    {
        foreach (var pluginName in pluginNames)
        {
            try
            {
                LoadPlugin(pluginName);
            }
            catch (Exception ex)
            {
                _logger.Warning("Failed to load plugin '{PluginName}': {Error}", pluginName, ex.Message);
            }
        }
    }

    /// <summary>
    /// Loads a single plugin by name.
    /// </summary>
    /// <param name="pluginName">The plugin name.</param>
    private void LoadPlugin(string pluginName)
    {
        // Look for plugin DLL in plugin directory
        var pluginPath = Path.Combine(_pluginDirectory, pluginName, $"{pluginName}.dll");

        if (!File.Exists(pluginPath))
        {
            // Try looking in the current directory
            pluginPath = Path.Combine(Directory.GetCurrentDirectory(), "plugins", pluginName, $"{pluginName}.dll");
        }

        if (!File.Exists(pluginPath))
        {
            _logger.Warning("Plugin '{PluginName}' not found at expected locations", pluginName);
            return;
        }

        _logger.Debug("Loading plugin from '{PluginPath}'", pluginPath);

        var assembly = Assembly.LoadFrom(pluginPath);
        var providerTypes = assembly.GetTypes()
            .Where(t => typeof(ITaskTypeProvider).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var providerType in providerTypes)
        {
            try
            {
                var provider = (ITaskTypeProvider?)Activator.CreateInstance(providerType);
                if (provider != null)
                {
                    RegisterProvider(provider);
                    _logger.Info("Loaded task type provider '{TaskType}' from plugin '{PluginName}'", provider.TaskType, pluginName);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning("Failed to instantiate provider '{ProviderType}': {Error}", providerType.Name, ex.Message);
            }
        }
    }

    /// <summary>
    /// Registers a task type provider directly (for built-in or programmatic registration).
    /// </summary>
    /// <param name="provider">The provider to register.</param>
    public void RegisterProvider(ITaskTypeProvider provider)
    {
        if (_providers.ContainsKey(provider.TaskType))
        {
            _logger.Warning("Task type '{TaskType}' already registered, overwriting", provider.TaskType);
        }

        _providers[provider.TaskType] = provider;
    }

    /// <summary>
    /// Gets a provider for the specified task type.
    /// </summary>
    /// <param name="taskType">The task type.</param>
    /// <returns>The provider, or null if not found.</returns>
    public ITaskTypeProvider? GetProvider(string taskType)
    {
        return _providers.TryGetValue(taskType, out var provider) ? provider : null;
    }

    /// <summary>
    /// Checks if a provider exists for the specified task type.
    /// </summary>
    /// <param name="taskType">The task type.</param>
    /// <returns>True if a provider exists.</returns>
    public bool HasProvider(string taskType)
    {
        return _providers.ContainsKey(taskType);
    }

    /// <summary>
    /// Gets a list of all registered task types.
    /// </summary>
    /// <returns>Collection of task type names.</returns>
    public IEnumerable<string> GetRegisteredTaskTypes()
    {
        // Include built-in types
        return new[] { "shell", "process" }.Concat(_providers.Keys);
    }
}
