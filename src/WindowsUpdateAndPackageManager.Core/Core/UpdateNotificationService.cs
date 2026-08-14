using System;
using System.Threading;
using System.Threading.Tasks;
using WindowsUpdateAndPackageManager.Core;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class UpdateNotificationService : IUpdateNotificationService, IAsyncDisposable
{
    private readonly IRepoSync _repoSync;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public UpdateNotificationService(IRepoSync repoSync)
    {
        _repoSync = repoSync;
    }

    public Task StartAsync(TimeSpan interval, CancellationToken cancellationToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = RunAsync(interval, _cts.Token);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _cts?.Cancel();
        return _loop ?? Task.CompletedTask;
    }

    private async Task RunAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _repoSync.ListAsync("https://github.com/LoopyLuci/WindowsUpdateAndPackageManager");
            }
            catch
            {
                // ignore transient notification check failures
            }

            try
            {
                await Task.Delay(interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
