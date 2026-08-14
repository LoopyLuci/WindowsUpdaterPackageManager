#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Data;
using WindowsUpdateAndPackageManager.Infrastructure;
using WindowsUpdateAndPackageManager.Models;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class PackageManagerTests
{
    [Fact]
    public async Task InstallAsync_returns_success_when_already_installed()
    {
        var state = new FakeStateDatabase();
        var pkg = new PackageManifest { Id = "pkg1", Version = "1.0" };
        await state.RecordInstallAsync(pkg);
        var manager = new PackageManager(state, new FakeAuditStore(), new FakeCacheManager(), new FakePolicyEngine(true));

        var result = await manager.InstallAsync(pkg);

        Assert.True(result.Success);
        Assert.Equal("1.0", result.InstalledVersion);
        Assert.Equal("Package is already installed.", result.Message);
    }

    [Fact]
    public async Task InstallAsync_blocks_when_policy_denies()
    {
        var state = new FakeStateDatabase();
        var manager = new PackageManager(state, new FakeAuditStore(), new FakeCacheManager(), new FakePolicyEngine(false));

        var result = await manager.InstallAsync(new PackageManifest { Id = "pkg1", Version = "1.0" });

        Assert.False(result.Success);
        Assert.Contains("blocked", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InstallAsync_returns_not_cached_when_missing()
    {
        var state = new FakeStateDatabase();
        var manager = new PackageManager(state, new FakeAuditStore(), new FakeCacheManager(cached: false), new FakePolicyEngine(true));

        var result = await manager.InstallAsync(new PackageManifest { Id = "pkg1", Version = "1.0" });

        Assert.False(result.Success);
        Assert.Contains("sync", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UninstallAsync_returns_not_installed_when_missing()
    {
        var state = new FakeStateDatabase();
        var manager = new PackageManager(state, new FakeAuditStore(), new FakeCacheManager(), new FakePolicyEngine(true));

        var result = await manager.UninstallAsync("pkg1");

        Assert.False(result.Success);
        Assert.Contains("not installed", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InstallAsync_records_audit_entry()
    {
        var state = new FakeStateDatabase();
        var audit = new FakeAuditStore();
        var manager = new PackageManager(state, audit, new FakeCacheManager(), new FakePolicyEngine(true));

        await manager.InstallAsync(new PackageManifest { Id = "pkg1", Version = "1.0" });

        Assert.Single(audit.Entries);
        Assert.Equal("Package.Install", audit.Entries[0].Action);
    }

    private sealed class FakeStateDatabase : IStateDatabase
    {
        private readonly Dictionary<string, PackageManifest> _installed = new();
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PackageManifest>> ListInstalledAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PackageManifest>>(_installed.Values.ToList());
        public Task RecordInstallAsync(PackageManifest package, CancellationToken cancellationToken = default)
        {
            _installed[$"{package.Id}@{package.Version}"] = package;
            return Task.CompletedTask;
        }
        public Task RemoveInstallAsync(string packageId, CancellationToken cancellationToken = default)
        {
            var key = _installed.Keys.FirstOrDefault(k => k.StartsWith(packageId + "@", StringComparison.OrdinalIgnoreCase));
            if (key is not null) _installed.Remove(key);
            return Task.CompletedTask;
        }
        public Task<bool> IsInstalledAsync(string packageId, string? version = null, CancellationToken cancellationToken = default)
        {
            var key = string.IsNullOrWhiteSpace(version) ? packageId : $"{packageId}@{version}";
            return Task.FromResult(_installed.ContainsKey(key));
        }
    }

    private sealed class FakeAuditStore : IAuditStore
    {
        public List<AuditEntry> Entries { get; } = new();
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<AuditEntry>> QueryAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, string? action = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditEntry>>(Entries);
    }

    private sealed class FakeCacheManager : ICacheManager
    {
        private readonly bool _cached;
        public FakeCacheManager(bool cached = true) => _cached = cached;
        public Task<string> GetCacheRootAsync(CancellationToken cancellationToken = default) => Task.FromResult("cache");
        public Task<string> EnsurePackageCacheAsync(string packageId, string version, CancellationToken cancellationToken = default) => Task.FromResult("cache");
        public Task<bool> IsCachedAsync(string packageId, string version, CancellationToken cancellationToken = default) => Task.FromResult(_cached);
        public Task PruneAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateAsync(string packageId, string version, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakePolicyEngine : IPolicyEngine
    {
        private readonly bool _allow;
        public FakePolicyEngine(bool allow) => _allow = allow;
        public Task<bool> IsAllowedAsync(string packageId, CancellationToken cancellationToken = default) => Task.FromResult(_allow);
        public Task<bool> ShouldBlockRebootAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task ApplyAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> VerifyDriverAsync(Models.DriverPackageManifest manifest, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
