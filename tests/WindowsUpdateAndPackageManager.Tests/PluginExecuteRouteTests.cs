using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using WindowsUpdateAndPackageManager.Core;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public class PluginExecuteRouteTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "wupm-content-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);

        var pluginDir = FindSamplePluginSourceDir();
        var sourceDll = Directory.GetFiles(pluginDir, "SamplePlugin.dll", SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new FileNotFoundException("SamplePlugin.dll not found. Build src/plugins/SamplePlugin first.");

        var pluginsRoot = Path.Combine(contentRoot, ".wupm", "plugins");
        Directory.CreateDirectory(pluginsRoot);
        File.Copy(sourceDll, Path.Combine(pluginsRoot, "SamplePlugin.dll"));

        var registry = new JsonObject
        {
            ["plugins"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "SamplePlugin",
                    ["assemblyPath"] = Path.Combine(pluginsRoot, "SamplePlugin.dll"),
                    ["enabled"] = true
                }
            }
        };
        await File.WriteAllTextAsync(Path.Combine(contentRoot, ".wupm", "registry.json"), registry.ToJsonString());

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Testing");
                builder.UseContentRoot(contentRoot);
            });

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost:5002") });

        using var health = new HttpClient();
        var healthUrl = _client.BaseAddress + "health";
        for (var i = 0; i < 20; i++)
        {
            try
            {
                var response = await health.GetAsync(healthUrl);
                if (response.StatusCode == HttpStatusCode.OK) break;
            }
            catch
            {
                // ignore until ready
            }
            await Task.Delay(500);
        }
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is IAsyncDisposable ad) await ad.DisposeAsync();
        else _factory?.Dispose();
    }

    [Fact]
    public async Task Plugins_execute_returns_output_when_command_exists()
    {
        Assert.NotNull(_client);
        var payload = new JsonObject { ["command"] = "hello", ["args"] = "" };
        using var response = await _client.PostAsync("/plugins/SamplePlugin/execute", new StringContent(payload.ToJsonString(), System.Text.Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var json = JsonNode.Parse(body)!;
        Assert.Equal("Hello from SamplePlugin!", json["output"]?.GetValue<string>());
    }

    [Fact]
    public async Task Plugins_execute_returns_not_found_when_plugin_missing()
    {
        Assert.NotNull(_client);
        var payload = new JsonObject { ["command"] = "hello", ["args"] = "" };
        using var response = await _client.PostAsync("/plugins/MissingPlugin/execute", new StringContent(payload.ToJsonString(), System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static string FindSamplePluginSourceDir()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "plugins", "SamplePlugin", "bin", "Release");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate src/plugins/SamplePlugin/bin/Release from test execution path.");
    }
}
