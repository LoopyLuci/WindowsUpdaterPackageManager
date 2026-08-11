namespace WindowsUpdateAndPackageManager.Core;

public interface IDismProcessRunner
{
    Task<int> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default);
}
