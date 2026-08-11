using System.Diagnostics;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class DefaultDismProcessRunner : IDismProcessRunner
{
    public async Task<int> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi);
        if (p is null) throw new InvalidOperationException("Failed to start DISM.");
        await p.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return p.ExitCode;
    }
}
