using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Tools;

namespace WupmMcp.Tools;

public sealed class PluginsExecuteTool : McpTool
{
    private readonly WupmApiClient _api;

    public PluginsExecuteTool(WupmApiClient api) : base("plugins_execute", "Execute a plugin command", new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["name"] = new JsonObject { ["type"] = "string", ["description"] = "Plugin name" },
            ["command"] = new JsonObject { ["type"] = "string", ["description"] = "Command to run" },
            ["args"] = new JsonObject { ["type"] = "string", ["description"] = "Optional command arguments" }
        },
        ["required"] = new JsonArray { "name", "command" }
    })
    {
        _api = api;
    }

    public override async Task<JsonNode> ExecuteAsync(JsonNode? parameters, CancellationToken ct)
    {
        var name = parameters?["name"]?.GetValue<string>() ?? string.Empty;
        var command = parameters?["command"]?.GetValue<string>() ?? string.Empty;
        var args = parameters?["args"]?.GetValue<string>() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(command))
        {
            return JsonNode.Parse("{\"error\":\"name and command are required\"}")!;
        }

        try
        {
            return await _api.PluginExecuteAsync(name, command, args, ct);
        }
        catch (Exception ex)
        {
            return JsonNode.Parse($"{{\"error\":\"{ex.Message}\"}}")!;
        }
    }
}
