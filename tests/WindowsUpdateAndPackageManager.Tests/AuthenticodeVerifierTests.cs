using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class AuthenticodeVerifierTests
{
    [Fact]
    public void Verify_returns_false_when_file_missing()
    {
        var verifier = new Infrastructure.AuthenticodeVerifier();
        Assert.False(verifier.Verify("C:\\nonexistent\\file.wupkg"));
    }
}
