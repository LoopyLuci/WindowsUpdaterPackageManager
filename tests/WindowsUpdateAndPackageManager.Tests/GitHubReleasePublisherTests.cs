using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class GitHubReleasePublisherTests
{
    [Fact]
    public async Task PublishReleaseAsync_returns_false_when_api_returns_failure()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        });
        using var http = new HttpClient(handler);
        var publisher = new WindowsUpdateAndPackageManager.Core.GitHubReleasePublisher(http);
        var result = await publisher.PublishReleaseAsync("owner", "repo", "v1.0.0", Path.GetTempFileName());
        Assert.False(result);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }
}
