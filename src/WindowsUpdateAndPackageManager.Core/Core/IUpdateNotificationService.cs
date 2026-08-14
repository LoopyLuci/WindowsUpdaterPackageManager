using System;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsUpdateAndPackageManager.Core;

public interface IUpdateNotificationService
{
    Task StartAsync(TimeSpan interval, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    bool IsRunning { get; }
}
