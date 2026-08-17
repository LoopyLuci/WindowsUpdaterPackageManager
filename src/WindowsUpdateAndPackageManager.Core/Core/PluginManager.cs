using System.Reflection;
using System.Runtime.Loader;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class PluginManager
{
    private readonly List<IPlugin> _plugins = new();
    private readonly string _pluginsRoot;

    static PluginManager()
    {
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            try
            {
                var assemblyName = name.Name ?? string.Empty;
                var simpleName = new AssemblyName(assemblyName).Name;
                if (string.IsNullOrWhiteSpace(simpleName)) return null;
                var candidate = Path.Combine(AppContext.BaseDirectory, $"{simpleName}.dll");
                if (File.Exists(candidate)) return context.LoadFromAssemblyPath(candidate);
            }
            catch
            {
                // ignore resolution failures
            }
            return null;
        };
    }

    public PluginManager(string pluginsRoot)
    {
        _pluginsRoot = pluginsRoot;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_pluginsRoot)) return;

        var pluginFiles = Directory.EnumerateFiles(_pluginsRoot, "*.dll", SearchOption.TopDirectoryOnly).ToList();
        foreach (var file in pluginFiles)
        {
            Assembly asm;
            try
            {
                using var loadCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, loadCts.Token);
                asm = await Task.Run(() => AssemblyLoadContext.Default.LoadFromAssemblyPath(file), linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                continue;
            }
            catch (Exception)
            {
                continue;
            }

            Type[] types;
            try
            {
                using var typeCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var linkedTypeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, typeCts.Token);
                types = await Task.Run(() => asm.GetTypes(), linkedTypeCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                continue;
            }
            catch (Exception)
            {
                continue;
            }

            var pluginType = types.FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);
            if (pluginType is null) continue;

            try
            {
                if (Activator.CreateInstance(pluginType) is IPlugin plugin)
                {
                    using var initCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    using var linkedInitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, initCts.Token);
                    await plugin.InitializeAsync(linkedInitCts.Token).ConfigureAwait(false);
                    _plugins.Add(plugin);
                }
            }
            catch (Exception)
            {
                continue;
            }
        }
    }

    public IReadOnlyList<IPlugin> Plugins => _plugins;
}
