using System.Threading.Tasks;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public class RepositoryIndexParserTests
{
    [Fact]
    public async Task Validates_good_index()
    {
        var json = """
        {
          "schemaVersion": "1.0",
          "generatedAt": "2026-08-10T00:00:00Z",
          "repositoryUrl": "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager",
          "packages": [
            {
              "id": "pkg",
              "version": "1.0",
              "sha256": "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF",
              "created": "2026-08-10T00:00:00Z"
            }
          ]
        }
        """;
        var validator = new WindowsUpdateAndPackageManager.Infrastructure.DefaultManifestValidator();
        Assert.True(await validator.ValidateAsync(json));
    }

    [Fact]
    public async Task Rejects_missing_schema()
    {
        var json = "{}";
        var validator = new WindowsUpdateAndPackageManager.Infrastructure.DefaultManifestValidator();
        Assert.False(await validator.ValidateAsync(json));
    }
}
