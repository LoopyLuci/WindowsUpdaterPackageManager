namespace WindowsUpdateAndPackageManager.Core;

public interface IProcessRunner
{
    Task<int> StartAndWaitAsync(string fileName, string arguments, CancellationToken cancellationToken = default);
}
