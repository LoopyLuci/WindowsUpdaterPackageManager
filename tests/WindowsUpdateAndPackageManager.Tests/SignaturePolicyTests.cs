using Microsoft.Extensions.DependencyInjection;
using Moq;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Data;
using WindowsUpdateAndPackageManager.Infrastructure;
using WindowsUpdateAndPackageManager.Models;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class SignaturePolicyTests
{
    [Fact]
    public async Task PackageManager_blocks_install_when_signature_verifier_fails()
    {
        var cache = new FakeCacheManager(true);
        var state = new FakeStateDatabase();
        var audit = new FakeAuditStore();
        var policy = new FakePolicyEngine(true);
        var verifier = new FakeSignatureVerifier(false);

        var manager = new PackageManager(state, audit, cache, policy, verifier);
        var result = await manager.InstallAsync(new PackageManifest { Id = "pkg", Version = "1.0", InstallCommand = "notepad.exe" });
        Assert.False(result.Success);
        Assert.Contains("signature", result.Message!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AuthenticodeVerifier_returns_false_for_unsigned_file()
    {
        var verifier = new AuthenticodeVerifier(new SignaturePolicyOptions { AllowUntrusted = false });
        var result = verifier.Verify(Path.GetTempFileName());
        Assert.False(result);
    }

    [Fact]
    public void AuthenticodeVerifier_returns_true_when_allow_untrusted()
    {
        var verifier = new AuthenticodeVerifier(new SignaturePolicyOptions { AllowUntrusted = true });
        var result = verifier.Verify(Path.GetTempFileName());
        Assert.True(result);
    }

    private sealed class FakeCacheManager : ICacheManager
    {
        private readonly bool _cached;
        public FakeCacheManager(bool cached) => _cached = cached;
        public Task<string> GetCacheRootAsync(CancellationToken cancellationToken = default) => Task.FromResult(Path.GetTempPath());
        public Task<string> EnsurePackageCacheAsync(string packageId, string version, CancellationToken cancellationToken = default) => Task.FromResult(Path.Combine(Path.GetTempPath(), packageId, version));
        public Task<bool> IsCachedAsync(string packageId, string version, CancellationToken cancellationToken = default) => Task.FromResult(_cached);
        public Task PruneAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InvalidateAsync(string packageId, string version, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> ListCachedAsync(string? packageId = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>(new List<string>());
    }

    private sealed class FakeStateDatabase : IStateDatabase
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PackageManifest>> ListInstalledAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PackageManifest>>(new List<PackageManifest>());
        public Task<bool> IsInstalledAsync(string packageId, string? version = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task RecordInstallAsync(PackageManifest package, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveInstallAsync(string packageId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeAuditStore : IAuditStore
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<AuditEntry>> QueryAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, string? action = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AuditEntry>>(new List<AuditEntry>());
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

    private sealed class FakeSignatureVerifier : ISignatureVerifier
    {
        private readonly bool _valid;
        public FakeSignatureVerifier(bool valid) => _valid = valid;
        public bool Verify(string filePath) => _valid;
    }
}
