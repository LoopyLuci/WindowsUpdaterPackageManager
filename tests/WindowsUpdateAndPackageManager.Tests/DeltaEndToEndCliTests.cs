using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WindowsUpdateAndPackageManager.Commands;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Data;
using WindowsUpdateAndPackageManager.Infrastructure;
using WindowsUpdateAndPackageManager.Models;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

[Collection("Console")]
public sealed class DeltaEndToEndCliTests
{
    private sealed class FakeDeltaStore : IDeltaStore
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(DeltaManifest manifest, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<DeltaManifest?> GetAsync(string packageId, string fromVersion, string toVersion, CancellationToken cancellationToken = default)
            => Task.FromResult<DeltaManifest?>(new DeltaManifest { PackageId = packageId, FromVersion = fromVersion, ToVersion = "2.0", DeltaUrl = "https://example.invalid/delta", DeltaSize = 5, DeltaHash = "abc" });
        public Task<IReadOnlyList<DeltaManifest>> ListAsync(string packageId, string? toVersion = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DeltaManifest>>(new List<DeltaManifest>());
    }

    private sealed class FakeDeltaProvider : IPackageDeltaProvider
    {
        private readonly IDeltaStore _store;
        private readonly IRepoClient _repo;
        private readonly ICacheManager _cache;
        public FakeDeltaProvider(IDeltaStore store) { _store = store; _repo = new FakeRepoClient(); _cache = new FakeCacheManager(); }
        public Task<DeltaManifest?> GetDeltaAsync(string packageId, string fromVersion, string toVersion, CancellationToken cancellationToken = default)
            => _store.GetAsync(packageId, fromVersion, toVersion, cancellationToken);
        public async Task<bool> ApplyDeltaAsync(string packageId, string fromVersion, string toVersion, CancellationToken cancellationToken = default)
        {
            var delta = await GetDeltaAsync(packageId, fromVersion, toVersion, cancellationToken);
            if (delta is null) return false;
            await using var stream = await _repo.DownloadPackageAsync(delta.DeltaUrl, cancellationToken);
            var targetDir = await _cache.EnsurePackageCacheAsync(packageId, toVersion, cancellationToken);
            Directory.CreateDirectory(targetDir);
            var targetPath = Path.Combine(targetDir, $"{packageId}@{toVersion}.wupkg");
            await using (var target = File.Create(targetPath)) await stream.CopyToAsync(target, cancellationToken);
            return true;
        }
    }

    private sealed class FakeRepoClient : IRepoClient
    {
        public Task<string> DownloadIndexAsync(string repositoryUrl, CancellationToken cancellationToken = default)
            => Task.FromResult("{\"packages\":[]}");
        public Task<Stream> DownloadPackageAsync(string packageUrl, CancellationToken cancellationToken = default)
        {
            var payload = Encoding.UTF8.GetBytes("hello");
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(payload);
            return Task.FromResult<Stream>(new MemoryStream(payload));
        }
        public Task<string> GetLatestReleaseAsync(string repositoryUrl, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
        public Task<string> DownloadFileAsync(string fileUrl, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
    }

    private sealed class FakeCacheManager : ICacheManager
    {
        public Task<string> GetCacheRootAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Path.Combine(Path.GetTempPath(), "wupm-cache"));
        public Task<string> EnsurePackageCacheAsync(string packageId, string version, CancellationToken cancellationToken = default)
            => Task.FromResult(Path.Combine(Path.GetTempPath(), "wupm-cache", packageId, version));
        public Task<bool> IsCachedAsync(string packageId, string version, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
        public Task PruneAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> ListCachedAsync(string? packageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(new List<string>());
    }

    private sealed class FakeStateDatabase : IStateDatabase
    {
        private readonly List<PackageManifest> _installed = new();
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PackageManifest>> ListInstalledAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PackageManifest>>(_installed);
        public Task<bool> IsInstalledAsync(string packageId, string? version = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_installed.Exists(x => x.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase) && (version is null || x.Version == version)));
        public Task RecordInstallAsync(PackageManifest package, CancellationToken cancellationToken = default)
        {
            _installed.Add(package);
            return Task.CompletedTask;
        }
        public Task RemoveInstallAsync(string packageId, CancellationToken cancellationToken = default)
        {
            _installed.RemoveAll(x => x.Id.Equals(packageId, StringComparison.OrdinalIgnoreCase));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuditStore : IAuditStore
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<AuditEntry>> QueryAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, string? action = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditEntry>>(new List<AuditEntry>());
    }

    private sealed class FakePolicyEngine : IPolicyEngine
    {
        private readonly bool _allow;
        public FakePolicyEngine(bool allow) => _allow = allow;
        public Task<bool> IsAllowedAsync(string packageId, CancellationToken cancellationToken = default)
            => Task.FromResult(_allow);
        public Task<bool> ShouldBlockRebootAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task ApplyAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> VerifyDriverAsync(Models.DriverPackageManifest manifest, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class FakeSignatureVerifier : ISignatureVerifier
    {
        private readonly bool _valid;
        public FakeSignatureVerifier(bool valid) => _valid = valid;
        public bool Verify(string packagePath) => _valid;
    }

    private sealed class FakeRepoSync : IRepoSync
    {
        public Task<SyncResult> SyncAsync(string repositoryUrl, CancellationToken cancellationToken = default)
            => Task.FromResult(new SyncResult { Success = true, PackagesUpdated = 0, Message = "ok" });
        public Task<IReadOnlyList<PackageManifest>> ListAsync(string repositoryUrl, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PackageManifest>>(new List<PackageManifest>());
    }

    private sealed class FakeWindowsUpdateManager : IWindowsUpdateManager
    {
        public Task<WindowsUpdateResult> ScanAndInstallAsync(bool driversOnly = false, bool offlineScan = false, CancellationToken cancellationToken = default)
            => Task.FromResult(new WindowsUpdateResult { Success = true, UpdatesFound = 0, UpdatesInstalled = 0, RebootRequired = false, Message = "ok" });
    }

    private static IServiceProvider BuildServices(params (Type type, object instance)[] overrides)
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<IRepoSync>(new FakeRepoSync());
        collection.AddSingleton<IDeltaStore>(new FakeDeltaStore());
        collection.AddSingleton<IPackageDeltaProvider>(new FakeDeltaProvider(new FakeDeltaStore()));
        collection.AddSingleton<IRepoClient>(new FakeRepoClient());
        collection.AddSingleton<ICacheManager>(new FakeCacheManager());
        collection.AddSingleton<IStateDatabase>(new FakeStateDatabase());
        collection.AddSingleton<IAuditStore>(new FakeAuditStore());
        collection.AddSingleton<IPolicyEngine>(new FakePolicyEngine(true));
        collection.AddSingleton<ISignatureVerifier>(new FakeSignatureVerifier(true));
        collection.AddSingleton<IWindowsUpdateManager>(new FakeWindowsUpdateManager());
        foreach (var (type, instance) in overrides)
        {
            collection.AddSingleton(type, instance);
        }
        return collection.BuildServiceProvider();
    }

    [Fact]
    public async Task DeltaUpdate_end_to_end_apply_delta_then_install_succeeds()
    {
        var services = BuildServices();
        var output = new StringBuilder();
        using var writer = new StringWriter(output);
        var original = Console.Out;
        try
        {
            Console.SetOut(writer);
            await Task.Run(() => Cli.Run(new[] { "delta-update", "--id", "pkg", "--from", "1.0" }, services));
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Contains("Delta update applied successfully.", output.ToString());
    }

    [Fact]
    public async Task PackageDeltaProvider_round_trip_writes_cached_wupkg()
    {
        var payload = Encoding.UTF8.GetBytes("delta");
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(payload);
        var expectedHash = Convert.ToHexString(hash).ToLowerInvariant();

        var deltaStore = new Mock<IDeltaStore>();
        deltaStore.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeltaManifest { PackageId = "pkg", FromVersion = "1.0", ToVersion = "2.0", DeltaUrl = "https://example.invalid/delta", DeltaSize = payload.Length, DeltaHash = expectedHash });

        var repoClient = new Mock<IRepoClient>();
        repoClient.Setup(x => x.DownloadPackageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new MemoryStream(payload));

        var cache = new Mock<ICacheManager>();
        cache.Setup(x => x.EnsurePackageCacheAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Path.Combine(Path.GetTempPath(), "wupm-cache-test"));

        var provider = new PackageDeltaProvider(deltaStore.Object, repoClient.Object, cache.Object);
        var applyResult = await provider.ApplyDeltaAsync("pkg", "1.0", "2.0");
        Assert.True(applyResult);
        cache.Verify(x => x.EnsurePackageCacheAsync("pkg", "2.0", It.IsAny<CancellationToken>()), Times.Once);
    }
}
