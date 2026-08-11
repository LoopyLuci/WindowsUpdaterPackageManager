using System.Diagnostics;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class DefaultProcessRunner : IProcessRunner
{
    public async Task<int> StartAndWaitAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start process: {fileName}");
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode;
    }
}
