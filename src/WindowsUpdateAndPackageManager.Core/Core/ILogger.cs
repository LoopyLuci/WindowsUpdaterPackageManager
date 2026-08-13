namespace WindowsUpdateAndPackageManager.Core;

public interface ILogger
{
    void Log(string message);
    void LogError(string message);
    void LogWarning(string message);
}

public sealed class NullLogger : ILogger
{
    public static NullLogger Instance { get; } = new NullLogger();
    public void Log(string message) { }
    public void LogError(string message) { }
    public void LogWarning(string message) { }
    private NullLogger() { }
}
