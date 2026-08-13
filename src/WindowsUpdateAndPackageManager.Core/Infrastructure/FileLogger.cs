using System.IO;
using WindowsUpdateAndPackageManager.Core;

namespace WindowsUpdateAndPackageManager.Infrastructure;

public sealed class FileLogger : ILogger, IDisposable
{
    private readonly StreamWriter _writer;
    private bool _disposed;

    public FileLogger(string path)
    {
        _writer = new StreamWriter(path, append: true) { AutoFlush = true };
        Log("=== Logger started ===");
    }

    public void Log(string message)
    {
        WriteEntry("INFO", message);
    }

    public void LogError(string message)
    {
        WriteEntry("ERROR", message);
    }

    public void LogWarning(string message)
    {
        WriteEntry("WARN", message);
    }

    private void WriteEntry(string level, string message)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("o");
        _writer.WriteLine($"{timestamp} [{level}] {message}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Log("=== Logger stopped ===");
        _writer.Dispose();
    }
}
