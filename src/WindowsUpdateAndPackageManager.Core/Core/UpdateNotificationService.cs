using System;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsUpdateAndPackageManager.Core;

public sealed class UpdateNotificationService : IUpdateNotificationService, IAsyncDisposable
{
    private readonly IRepoSync _repoSync;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private int _running;

    public bool IsRunning => Volatile.Read(ref _running) == 1;

    public UpdateNotificationService(IRepoSync repoSync)
    {
        _repoSync = repoSync;
    }

    public Task StartAsync(TimeSpan interval, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _running, 1) == 1) return Task.CompletedTask;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loop = RunAsync(interval, _cts.Token);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _running, 0) == 0) return Task.CompletedTask;
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
