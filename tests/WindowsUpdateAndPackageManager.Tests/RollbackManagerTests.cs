#nullable enable
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Data;
using WindowsUpdateAndPackageManager.Infrastructure;
using WindowsUpdateAndPackageManager.Models;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class RollbackManagerTests
{
    [Fact]
    public async Task RollbackAsync_returns_false_when_no_installed_packages()
    {
        var state = new FakeStateDatabase();
        var manager = new RollbackManager(state, new FakeAuditStore());

        var result = await manager.RollbackAsync();

        Assert.False(result);
    }

    private sealed class FakeStateDatabase : IStateDatabase
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<PackageManifest>> ListInstalledAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PackageManifest>>(Array.Empty<PackageManifest>());
        public Task RecordInstallAsync(PackageManifest package, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveInstallAsync(string packageId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> IsInstalledAsync(string packageId, string? version = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
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
}
