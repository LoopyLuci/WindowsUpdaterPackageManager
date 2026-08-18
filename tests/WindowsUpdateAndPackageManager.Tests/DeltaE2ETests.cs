using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Data;
using WindowsUpdateAndPackageManager.Infrastructure;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class DeltaE2ETests
{
    [Fact]
    public async Task DeltaProvider_returns_null_when_delta_missing()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"wupm-delta-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);

        var services = new ServiceCollection();
        services.AddSingleton<IDeltaStore>(new FakeDeltaStore());
        services.AddSingleton<ICacheManager>(new DefaultCacheManager(temp));
        var sp = services.BuildServiceProvider();

        var provider = new PackageDeltaProvider(
            sp.GetRequiredService<IDeltaStore>(),
            new FakeRepoClient(),
            sp.GetRequiredService<ICacheManager>());

        var delta = await provider.GetDeltaAsync("missing", "1.0.0", "2.0.0");
        Assert.Null(delta);

        try { Directory.Delete(temp, recursive: true); } catch { }
    }

    private sealed class FakeDeltaStore : IDeltaStore
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<DeltaManifest?> GetAsync(string packageId, string fromVersion, string toVersion, CancellationToken ct = default)
            => Task.FromResult<DeltaManifest?>(null);
        public Task SaveAsync(DeltaManifest manifest, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<DeltaManifest>> ListAsync(string packageId, string? toVersion = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DeltaManifest>>(Array.Empty<DeltaManifest>());
    }

    private sealed class FakeRepoClient : IRepoClient
    {
        public Task<string> DownloadIndexAsync(string repositoryUrl, CancellationToken cancellationToken = default)
            => Task.FromResult("{}");
        public Task<Stream> DownloadPackageAsync(string packageUrl, CancellationToken cancellationToken = default)
            => Task.FromResult(Stream.Null);
        public Task<string> GetLatestReleaseAsync(string repositoryUrl, CancellationToken cancellationToken = default)
            => Task.FromResult("{}");
    }
}
