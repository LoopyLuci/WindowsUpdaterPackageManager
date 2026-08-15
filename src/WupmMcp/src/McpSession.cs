using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Protocol;
using WupmMcp.Tools;

namespace WupmMcp;

public sealed class McpSession
{
    private readonly McpTransport _transport;
    private readonly McpToolRegistry _tools;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public McpSession(McpTransport transport, McpToolRegistry tools)
    {
        _transport = transport;
        _tools = tools;
    }

    public async Task RunAsync()
    {
        while (true)
        {
            JsonNode? message = null;
            try
            {
                message = await _transport.ReadAsync(CancellationToken.None);
                Console.Error.WriteLine($"[MCP] received: {(message?.ToJsonString() ?? "null")}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MCP] read error: {ex.GetType().Name}: {ex.Message}");
                break;
            }

            if (message is null)
            {
                Console.Error.WriteLine("[MCP] EOF");
                break;
            }

            JsonNode? response = null;
            try
            {
                response = await HandleAsync(message);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[MCP] handle error: {ex.GetType().Name}: {ex.Message}");
            }

            if (response is not null)
            {
                try
                {
                    await _transport.WriteAsync(response, CancellationToken.None);
                    Console.Error.WriteLine($"[MCP] sent: {response.ToJsonString()}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[MCP] write error: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
    }

    private async Task<JsonNode?> HandleAsync(JsonNode message)
    {
        var request = JsonSerializer.Deserialize<McpRequest>(message.ToJsonString(), _jsonOptions);
        if (request is null) return null;

        // Notifications must not receive a response
        if (request.Id is null)
        {
            await Task.CompletedTask;
            return null;
        }

        return request.Method switch
        {
            "initialize" => new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = request.Id,
                ["result"] = new JsonObject
                {
                    ["protocolVersion"] = "2024-11-05",
                    ["capabilities"] = new JsonObject
                    {
                        ["tools"] = new JsonObject
                        {
                            ["listChanged"] = false
                        }
                    },
                    ["serverInfo"] = new JsonObject
                    {
                        ["name"] = "wupm-mcp",
                        ["version"] = "1.0.0"
                    }
                }
            },
            "tools/list" => _tools.ListTools(),
            "tools/call" => await HandleToolCallAsync(request, _jsonOptions),
            _ => new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = request.Id,
                ["error"] = new JsonObject
                {
                    ["code"] = -32601,
                    ["message"] = "Method not found"
                }
            }
        };
    }

    private async Task<JsonNode> HandleToolCallAsync(McpRequest request, JsonSerializerOptions options)
    {
        try
        {
            var parameters = request.Params;
            var arguments = parameters?["arguments"] as JsonObject;
            var name = parameters?["name"]?.ToString();
            if (string.IsNullOrWhiteSpace(name)) return McpResponse.Fail(request.Id, -32602, "Invalid params").ToJsonNode();

            var result = await _tools.InvokeAsync(name, arguments, CancellationToken.None);
            var content = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = result.ToJsonString() } };
            return McpResponse.Ok(request.Id, new JsonObject { ["content"] = content }).ToJsonNode();
        }
        catch (Exception ex)
        {
            return McpResponse.Fail(request.Id, -32603, ex.Message).ToJsonNode();
        }
    }
}
