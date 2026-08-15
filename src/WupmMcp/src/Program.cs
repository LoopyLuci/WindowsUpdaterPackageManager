using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WupmMcp.Protocol;
using WupmMcp.Tools;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<WupmApiClient>();
builder.Services.AddSingleton<McpToolRegistry>();
builder.Services.AddHostedService<McpHttpHost>();

var app = builder.Build();
app.MapGet("/", () => Results.Ok(new { status = "ok", server = "WupmMcp", transport = "http" }));
app.MapGet("/health", () => Results.Ok(new { status = "ok", server = "WupmMcp" }));

app.MapPost("/mcp", async (HttpRequest req, McpToolRegistry registry, CancellationToken ct) =>
{
    if (!req.ContentType?.Contains("application/json") ?? true)
    {
        return Results.BadRequest(new { error = "application/json required" });
    }

    JsonNode? message;
    try
    {
        message = await JsonNode.ParseAsync(req.Body, cancellationToken: ct);
    }
    catch
    {
        return Results.BadRequest(new { error = "invalid json" });
    }

    if (message is null)
    {
        return Results.BadRequest(new { error = "empty body" });
    }

    var obj = message as JsonObject;
    if (obj is null)
    {
        return Results.BadRequest(new { error = "expected json object" });
    }

    var id = obj["id"];
    var method = obj["method"]?.ToString();
    var parameters = (obj["params"] as JsonObject) ?? new JsonObject();

    if (string.IsNullOrEmpty(method) || method == "notifications/initialized" || method == "notifications/cancelled")
    {
        return Results.NoContent();
    }

    JsonNode? result = method switch
    {
        "initialize" => new JsonObject
        {
            ["protocolVersion"] = "2024-11-05",
            ["capabilities"] = new JsonObject
            {
                ["tools"] = new JsonObject { ["listChanged"] = false }
            },
            ["serverInfo"] = new JsonObject
            {
                ["name"] = "wupm",
                ["version"] = "1.0.0"
            }
        },
        "tools/list" => BuildToolList(registry),
        "tools/call" => await registry.InvokeAsync(GetString(parameters, "name") ?? string.Empty, parameters, ct),
        _ => new JsonObject { ["error"] = new JsonObject { ["code"] = -32601, ["message"] = "method not found" } }
    };

    var response = new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id.DeepClone(),
        ["result"] = result.DeepClone()
    };

    return Results.Content(response.ToJsonString(), "application/json");

    static JsonNode BuildToolList(McpToolRegistry registry)
    {
        var arr = new JsonArray();
        foreach (var tool in registry.All.Values)
        {
            arr.Add(new JsonObject
            {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["inputSchema"] = tool.InputSchema.DeepClone()
            });
        }
        return new JsonObject { ["tools"] = arr };
    }

    static string? GetString(JsonNode? node, string key)
    {
        if (node is not JsonObject obj || !obj.TryGetPropertyValue(key, out var value))
        {
            return null;
        }
        return value?.ToString();
    }
});

app.Run();

public class McpHttpHost : IHostedService
{
    private readonly IHostApplicationLifetime _lifetime;

    public McpHttpHost(IHostApplicationLifetime lifetime)
    {
        _lifetime = lifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Console.Error.WriteLine("[MCP] HTTP server starting on http://127.0.0.1:9473/mcp");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
