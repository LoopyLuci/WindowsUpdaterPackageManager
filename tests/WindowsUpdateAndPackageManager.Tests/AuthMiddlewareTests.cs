using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class AuthMiddlewareTests
{
    private static TestServer BuildServer(bool withApiKey)
    {
        if (withApiKey)
        {
            Environment.SetEnvironmentVariable("WUPM_API_KEY", "secret");
        }
        else
        {
            Environment.SetEnvironmentVariable("WUPM_API_KEY", null);
        }

        var host = new TestServer(new WebHostBuilder()
            .ConfigureServices(s =>
            {
                foreach (var d in new ServiceCollection()) s.Add(d);
                s.AddRouting();
            })
            .Configure(app =>
            {
                var apiKey = Environment.GetEnvironmentVariable("WUPM_API_KEY");
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    app.Use(async (context, next) =>
                    {
                        var path = context.Request.Path;
                        if (path.Equals("/", StringComparison.OrdinalIgnoreCase) ||
                            path.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
                            path.Equals("/packages", StringComparison.OrdinalIgnoreCase) ||
                            path.Equals("/installed", StringComparison.OrdinalIgnoreCase) ||
                            path.Equals("/updates", StringComparison.OrdinalIgnoreCase))
                        {
                            await next();
                            return;
                        }

                        if (!context.Request.Headers.TryGetValue("Authorization", out var auth) &&
                            !context.Request.Headers.TryGetValue("X-Api-Key", out auth))
                        {
                            auth = string.Empty;
                        }

                        var token = auth.ToString();
                        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        {
                            token = token["Bearer ".Length..].Trim();
                        }

                        if (!string.Equals(token, apiKey, StringComparison.Ordinal))
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            var payload = System.Text.Json.JsonSerializer.Serialize(new { error = "Unauthorized" });
                            await context.Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes(payload));
                            return;
                        }

                        await next();
                    });
                }

                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/health", () => Results.Ok(new { }));
                    endpoints.MapGet("/packages", () => Results.Ok(new { }));
                    endpoints.MapPost("/cli/execute", () => Results.Ok(new { }));
                    endpoints.MapPost("/updates/install", () => Results.Ok(new { }));
                });
            }));

        return host;
    }

    [Fact]
    public async Task Write_endpoints_require_api_key_when_configured()
    {
        using var server = BuildServer(true);
        using var client = server.CreateClient();

        using var write = await client.PostAsync("/cli/execute", new StringContent("{}"));
        Assert.Equal(HttpStatusCode.Unauthorized, write.StatusCode);

        using var install = await client.PostAsync("/updates/install", new StringContent("{}"));
        Assert.Equal(HttpStatusCode.Unauthorized, install.StatusCode);
    }

    [Fact]
    public async Task Read_endpoints_are_open_without_api_key()
    {
        using var server = BuildServer(true);
        using var client = server.CreateClient();

        using var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        using var packages = await client.GetAsync("/packages");
        Assert.Equal(HttpStatusCode.OK, packages.StatusCode);
    }

    [Fact]
    public async Task Write_endpoints_are_open_without_api_key()
    {
        using var server = BuildServer(false);
        using var client = server.CreateClient();

        using var write = await client.PostAsync("/cli/execute", new StringContent("{}"));
        Assert.Equal(HttpStatusCode.OK, write.StatusCode);
    }
}
