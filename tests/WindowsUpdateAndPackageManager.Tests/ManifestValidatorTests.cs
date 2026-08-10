namespace WindowsUpdateAndPackageManager.Tests;

public class RepositoryIndexParserTests
{
    [Fact]
    public void Validates_good_index()
    {
        var json = """
        {
          "schemaVersion": "1.0",
          "generatedAt": "2026-08-10T00:00:00Z",
          "repositoryUrl": "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager",
          "packages": []
        }
        """;
        var validator = new WindowsUpdateAndPackageManager.Infrastructure.DefaultManifestValidator();
        Assert.True(validator.ValidateAsync(json).GetAwaiter().GetResult());
    }

    [Fact]
    public void Rejects_missing_schema()
    {
        var json = "{}";
        var validator = new WindowsUpdateAndPackageManager.Infrastructure.DefaultManifestValidator();
        Assert.False(validator.ValidateAsync(json).GetAwaiter().GetResult());
    }
}
