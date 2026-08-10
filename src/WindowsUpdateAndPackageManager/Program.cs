using System.Reflection;
using WindowsUpdateAndPackageManager.Commands;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootPath = AppContext.BaseDirectory;
        var services = Composition.Build(rootPath);
        try
        {
            return await Cli.Run(args, services);
        }
        finally
        {
            if (services is IDisposable d) d.Dispose();
        }
    }
}
