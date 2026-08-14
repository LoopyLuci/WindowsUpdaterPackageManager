using System.Reflection;
using System.Runtime.Loader;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class PluginManager
{
    private readonly List<IPlugin> _plugins = new();
    private readonly string _pluginsRoot;

    public PluginManager(string pluginsRoot)
    {
        _pluginsRoot = pluginsRoot;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_pluginsRoot)) return;

        foreach (var file in Directory.EnumerateFiles(_pluginsRoot, "*.dll", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
                var pluginType = asm.GetTypes().FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);
                if (pluginType is null) continue;

                if (Activator.CreateInstance(pluginType) is IPlugin plugin)
                {
                    await plugin.InitializeAsync(cancellationToken).ConfigureAwait(false);
                    _plugins.Add(plugin);
                }
            }
            catch
            {
                // Skip broken plugins without crashing.
            }
        }
    }

    public IReadOnlyList<IPlugin> Plugins => _plugins;
}
