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
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var tcs = new TaskCompletionSource<bool>();

        process.Exited += (_, __) => tcs.TrySetResult(true);
        if (!process.Start()) throw new InvalidOperationException($"Failed to start process: {fileName}");

        using var registration = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(true); }
            catch { }
            tcs.TrySetCanceled(cancellationToken);
        });

        await tcs.Task;
        return process.ExitCode;
    }
}
