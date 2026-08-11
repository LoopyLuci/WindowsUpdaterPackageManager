using WindowsUpdateAndPackageManager.Infrastructure;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class RepositoryIndexSchemaTests
{
    [Theory]
    [InlineData("1.0")]
    [InlineData("1.0.0")]
    [InlineData("2")]
    public async Task Validates_supported_schema_versions(string version)
    {
        var json = $$"""
        {
          "schemaVersion": "{{version}}",
          "generatedAt": "2026-08-10T00:00:00Z",
          "repositoryUrl": "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager",
          "packages": []
        }
        """;

        var result = await DefaultManifestValidator.ValidateAsync(json);
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("0.9")]
    [InlineData("3")]
    [InlineData("next")]
    [InlineData("")]
    public async Task Rejects_unknown_schema_versions(string version)
    {
        var json = $$"""
        {
          "schemaVersion": "{{version}}",
          "generatedAt": "2026-08-10T00:00:00Z",
          "repositoryUrl": "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager",
          "packages": []
        }
        """;

        var result = await DefaultManifestValidator.ValidateAsync(json);
        Assert.Null(result);
    }

    [Fact]
    public async Task Rejects_index_with_missing_schemaVersion()
    {
        var json = """
        {
          "generatedAt": "2026-08-10T00:00:00Z",
          "repositoryUrl": "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager",
          "packages": []
        }
        """;

        var result = await DefaultManifestValidator.ValidateAsync(json);
        Assert.Null(result);
    }
}
