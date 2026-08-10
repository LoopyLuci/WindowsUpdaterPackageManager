using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Moq;
using WindowsUpdateAndPackageManager.Infrastructure;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class RepoClientTests
{
    [Fact]
    public async Task DownloadIndexAsync_returns_index_text()
    {
        var mock = new Mock<IRepoClient>();
        mock.Setup(m => m.DownloadIndexAsync(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync("{\"schemaVersion\":\"1.0\",\"packages\":[]}");

        var client = mock.Object;
        var text = await client.DownloadIndexAsync("https://github.com/example/repo");
        Assert.Contains("schemaVersion", text);
    }

    [Fact]
    public async Task DownloadPackageAsync_returns_stream_with_content()
    {
        var mock = new Mock<IRepoClient>();
        mock.Setup(m => m.DownloadPackageAsync(It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("PK")));

        var client = mock.Object;
        await using var stream = await client.DownloadPackageAsync("https://example.com/package.zip");
        using var reader = new System.IO.StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        Assert.Equal("PK", content);
    }
}
