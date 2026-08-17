using System.Threading;
using System.Threading.Tasks;
using WindowsUpdateAndPackageManager.Core;

namespace SamplePlugin
{
    public class SamplePlugin : IPlugin
    {
        public string Name => "SamplePlugin";
        public string Version => "1.0.0";

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> GetCommandsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<string>>(new[] { "hello", "status" });
        }

        public Task<string?> ExecuteAsync(string command, string args, CancellationToken cancellationToken = default)
        {
            return command switch
            {
                "hello" => Task.FromResult<string?>("Hello from SamplePlugin!"),
                "status" => Task.FromResult<string?>("SamplePlugin is running."),
                _ => Task.FromResult<string?>(null)
            };
        }
    }
}
