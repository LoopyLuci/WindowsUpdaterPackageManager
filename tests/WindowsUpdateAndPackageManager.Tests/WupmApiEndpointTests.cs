using System.Net;
using System.Net.Sockets;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class WupmApiEndpointTests
{
    [Fact]
    public async Task Plugins_endpoint_returns_ok_when_api_is_available()
    {
        if (Environment.GetEnvironmentVariable("WUPM_API_TESTS") != "1") return;

        if (!IsPortAvailable(5002))
        {
            // API not running; skip without failing the suite.
            return;
        }

        using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5002/") };
        var response = await client.GetAsync("/plugins");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.NotNull(body);
    }

    [Fact]
    public async Task Health_endpoint_returns_ok_when_api_is_available()
    {
        if (Environment.GetEnvironmentVariable("WUPM_API_TESTS") != "1") return;

        if (!IsPortAvailable(5002))
        {
            return;
        }

        using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5002/") };
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
