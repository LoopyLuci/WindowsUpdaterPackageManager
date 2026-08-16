using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.Json.Nodes;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public class PluginsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PluginsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ASPNETCORE_URLS", "http://127.0.0.1:0");
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Plugins_endpoint_returns_json_array()
    {
        var response = await _client.GetAsync("/plugins");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("[", content.Trim());
        Assert.EndsWith("]", content.Trim());
    }

    [Fact]
    public async Task Plugins_health_returns_ok()
    {
        var response = await _client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonNode>();
        Assert.NotNull(json);
        Assert.Equal("ok", json?["status"]?.ToString());
    }
}
