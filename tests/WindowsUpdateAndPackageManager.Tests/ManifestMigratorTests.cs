using WindowsUpdateAndPackageManager.Core;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class ManifestMigratorTests
{
    [Fact]
    public async Task Migrate_upgrades_1_0_to_2()
    {
        var json = """
        {
          "schemaVersion": "1.0",
          "generatedAt": "2026-08-11T00:00:00Z",
          "repositoryUrl": "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager",
          "packages": []
        }
        """;

        var migrator = new ManifestMigrator();
        var result = await migrator.MigrateAsync(json, "2");
        Assert.NotNull(result);
        Assert.Contains("\"schemaVersion\":\"2\"", result);
        Assert.Contains("\"deltaAvailable\":false", result);
        Assert.Contains("\"previousSha256\":\"\"", result);
    }

    [Fact]
    public async Task Migrate_downgrades_2_to_1_0()
    {
        var json = """
        {
          "schemaVersion": "2",
          "generatedAt": "2026-08-11T00:00:00Z",
          "repositoryUrl": "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager",
          "packages": [],
          "deltaAvailable": true,
          "previousSha256": "abc"
        }
        """;

        var migrator = new ManifestMigrator();
        var result = await migrator.MigrateAsync(json, "1.0");
        Assert.NotNull(result);
        Assert.Contains("\"schemaVersion\":\"1.0\"", result);
        Assert.DoesNotContain("\"deltaAvailable\"", result);
        Assert.DoesNotContain("\"previousSha256\"", result);
    }

    [Fact]
    public async Task Migrate_returns_same_when_already_target_version()
    {
        var json = """
        {
          "schemaVersion": "1.0",
          "generatedAt": "2026-08-11T00:00:00Z",
          "repositoryUrl": "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager",
          "packages": []
        }
        """;

        var migrator = new ManifestMigrator();
        var result = await migrator.MigrateAsync(json, "1.0");
        Assert.NotNull(result);
        Assert.Contains("\"schemaVersion\":\"1.0\"", result);
        Assert.Contains("\"generatedAt\":\"2026-08-11T00:00:00Z\"", result);
        Assert.Contains("https://github.com/LoopyLuci/WindowsUpdateAndPackageManager", result);
    }

    [Fact]
    public async Task Migrate_returns_null_for_unsupported_transition()
    {
        var json = """
        {
          "schemaVersion": "1.0",
          "generatedAt": "2026-08-11T00:00:00Z",
          "repositoryUrl": "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager",
          "packages": []
        }
        """;

        var migrator = new ManifestMigrator();
        var result = await migrator.MigrateAsync(json, "9");
        Assert.Null(result);
    }

    [Fact]
    public async Task Migrate_returns_null_for_missing_schemaVersion()
    {
        var json = """
        {
          "generatedAt": "2026-08-11T00:00:00Z",
          "repositoryUrl": "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager",
          "packages": []
        }
        """;

        var migrator = new ManifestMigrator();
        var result = await migrator.MigrateAsync(json, "2");
        Assert.Null(result);
    }
}
