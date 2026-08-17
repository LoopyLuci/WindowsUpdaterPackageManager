using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WindowsUpdateAndPackageManager.Core;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class PluginExecuteRouteTests : IAsyncLifetime
{
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), "wupm-route-test-" + Guid.NewGuid().ToString("N"));
    private readonly string _pluginRoot = Path.Combine(Path.GetTempPath(), "wupm-plugin-route-test-" + Guid.NewGuid().ToString("N"));
    private WebApplication? _app;
    private HttpClient? _client;

    [Fact]
    public async Task ExecutePlugin_returns_output_from_real_plugin()
    {
        await using var server = new PluginExecuteServer(_contentRoot, _pluginRoot);
        await server.StartAsync();
        _client = server.Client;

        var response = await _client.PostAsync("http://localhost:5002/plugins/sample/execute",
            new StringContent("{\"command\":\"hello\",\"arguments\":{}}", Encoding.UTF8, "application/json"));

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("hello", body);
        Assert.Contains("sample", body);
    }

    [Fact]
    public async Task ExecutePlugin_returns_404_when_missing()
    {
        await using var server = new PluginExecuteServer(_contentRoot, _pluginRoot);
        await server.StartAsync();
        _client = server.Client;

        var response = await _client.PostAsync("http://localhost:5002/plugins/missing/execute",
            new StringContent("{\"command\":\"hello\",\"arguments\":{}}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_contentRoot);
        Directory.CreateDirectory(Path.Combine(_contentRoot, ".wupm", "plugins"));
        CopySamplePluginTo(Path.Combine(_contentRoot, ".wupm", "plugins"));
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        if (_app is not null)
        {
            return _app.DisposeAsync().AsTask();
        }
        return Task.CompletedTask;
    }

    private static void CopySamplePluginTo(string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        var sourceDir = FindSamplePluginSourceDir();
        var sourceDll = Directory.GetFiles(sourceDir, "SamplePlugin.dll", SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new InvalidOperationException("SamplePlugin.dll not found");
        File.Copy(sourceDll, Path.Combine(targetDir, "SamplePlugin.dll"), true);
        var sourceJson = Path.Combine(sourceDir, "registry.json");
        if (File.Exists(sourceJson))
        {
            File.Copy(sourceJson, Path.Combine(targetDir, "registry.json"), true);
        }
    }

    private static string FindSamplePluginSourceDir()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "plugins", "SamplePlugin");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("src/plugins/SamplePlugin not found");
    }

    private sealed class PluginExecuteServer : IAsyncDisposable
    {
        private readonly WebApplication _app;

        public PluginExecuteServer(string contentRoot, string pluginRoot)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = contentRoot,
                WebRootPath = contentRoot,
                Args = Array.Empty<string>(),
                EnvironmentName = Environments.Testing
            });

            builder.WebHost.UseKestrel();
            builder.WebHost.UseUrls("http://localhost:5002");

            var pluginsDir = Path.Combine(pluginRoot, "plugins");
            Directory.CreateDirectory(pluginsDir);
            var pluginManager = new PluginManager(pluginsDir);

            builder.Services.AddSingleton(pluginManager);

            _app = builder.Build();

            _app.MapPost("/plugins/{name}/execute", async (string name, JsonNode body, PluginManager pluginManager, CancellationToken ct) =>
            {
                var command = body?["command"]?.ToString();
                if (string.IsNullOrWhiteSpace(command))
                {
                    return Results.BadRequest(new { error = "command is required" });
                }

                var plugin = pluginManager.Plugins.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
                if (plugin is null)
                {
                    return Results.NotFound(new { name, error = "plugin not found" });
                }

                try
                {
                    var args = body?["arguments"]?.Deserialize<IReadOnlyDictionary<string, string>>() ?? new Dictionary<string, string>();
                    var output = await plugin.ExecuteAsync(command, args, ct);
                    return Results.Ok(new { name, command, args, output });
                }
                catch (Exception ex)
                {
                    return Results.Problem(detail: ex.Message, title: "plugin execution failed", statusCode: (int)HttpStatusCode.InternalServerError);
                }
            });
        }

        public HttpClient Client { get; } = new();

        public async Task StartAsync()
        {
            await _app.StartAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _app.DisposeAsync();
            Client.Dispose();
        }
    }
}
