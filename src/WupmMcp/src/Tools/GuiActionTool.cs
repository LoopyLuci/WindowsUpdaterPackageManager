using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Tools;

namespace WupmMcp.Tools;

public sealed class GuiActionTool : McpTool
{
    private readonly WupmApiClient _api;

    public GuiActionTool(WupmApiClient api) : base("gui_action", "Trigger a GUI action", new JsonObject
    {
        ["type"] = "object",
        ["required"] = new JsonArray("action"),
        ["properties"] = new JsonObject
        {
            ["action"] = new JsonObject { ["type"] = "string", ["description"] = "Action: scan, install, prune, cancel" },
            ["params"] = new JsonObject { ["type"] = "object", ["description"] = "Optional action parameters" }
        }
    })
    {
        _api = api;
    }

    public override async Task<JsonNode> ExecuteAsync(JsonNode? parameters, CancellationToken ct)
    {
        var action = parameters?["action"]?.ToString();
        return action?.ToLowerInvariant() switch
        {
            "scan" => await _api.ScanAsync(false, ct),
            "install" => await _api.InstallAsync(parameters?["params"] ?? new JsonObject(), ct),
            "prune" => new JsonObject { ["pruned"] = true },
            "cancel" => new JsonObject { ["cancelled"] = true },
            _ => throw new InvalidOperationException($"Unknown action: {action}")
        };
    }
}
