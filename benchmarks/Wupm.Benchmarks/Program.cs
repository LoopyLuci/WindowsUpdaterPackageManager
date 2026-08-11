using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Infrastructure;
using WindowsUpdateAndPackageManager.Models;

var count = 1000;
var packages = new List<PackageManifest>();
for (var i = 0; i < count; i++)
{
    packages.Add(new PackageManifest
    {
        Id = $"pkg-{i:D4}",
        Version = "1.0",
        Sha256 = RandomHex(64),
        Created = DateTimeOffset.UtcNow,
        InstallCommand = "notepad.exe"
    });
}

var index = new RepositoryIndex
{
    SchemaVersion = "1.0",
    GeneratedAt = DateTimeOffset.UtcNow,
    RepositoryUrl = "https://example.invalid/repo",
    Packages = packages
};

var json = System.Text.Json.JsonSerializer.Serialize(index);
var validator = new DefaultManifestValidator();

var sw = Stopwatch.StartNew();
var valid = await validator.ValidateAsync(json);
sw.Stop();
Console.WriteLine($"Validation: valid={valid}, elapsed={sw.ElapsedMilliseconds}ms");

var baseline = 500;
if (sw.ElapsedMilliseconds > baseline)
{
    Console.WriteLine($"FAIL: validation took {sw.ElapsedMilliseconds}ms, expected < {baseline}ms");
    Environment.Exit(1);
}

Console.WriteLine("PASS: performance baseline within threshold");

static string RandomHex(int length)
{
    var bytes = new byte[length / 2];
    RandomNumberGenerator.Fill(bytes);
    return Convert.ToHexString(bytes).ToLowerInvariant();
}
