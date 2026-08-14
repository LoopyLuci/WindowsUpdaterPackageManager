namespace WindowsUpdateAndPackageManager.Core;

public interface IPlugin
{
    string Name { get; }
    string Version { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetCommandsAsync(CancellationToken cancellationToken = default);
}
