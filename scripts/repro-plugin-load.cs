using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;

class Repro
{
    static async Task Main()
    {
        var pluginsRoot = @"D:\Projects\WindowsUpdatePackageManager\publish\sample-plugin";
        Console.WriteLine($"pluginsRoot={pluginsRoot}");
        if (!Directory.Exists(pluginsRoot))
        {
            Console.WriteLine("pluginsRoot missing");
            return;
        }

        foreach (var file in Directory.EnumerateFiles(pluginsRoot, "SamplePlugin.dll", SearchOption.TopDirectoryOnly))
        {
            Console.WriteLine($"found dll={file}");
            try
            {
                var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(file);
                var pluginType = asm.GetTypes().FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);
                Console.WriteLine($"pluginType={pluginType?.FullName}");
                if (pluginType is null) continue;
                var plugin = (IPlugin)Activator.CreateInstance(pluginType)!;
                Console.WriteLine($"Name={plugin.Name}");
                var commands = await plugin.GetCommandsAsync();
                Console.WriteLine($"Commands={string.Join(",", commands)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"load failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}

public interface IPlugin
{
    string Name { get; }
    Task<string[]> GetCommandsAsync(CancellationToken cancellationToken = default);
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
