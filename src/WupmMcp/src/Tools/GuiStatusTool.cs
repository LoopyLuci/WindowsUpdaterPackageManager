using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Tools;

namespace WupmMcp.Tools;

public sealed class GuiStatusTool : McpTool
{
    private readonly WupmApiClient _api;

    public GuiStatusTool(WupmApiClient api) : base("gui_status", "Get GUI and service status", new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject()
    })
    {
        _api = api;
    }

    public override async Task<JsonNode> ExecuteAsync(JsonNode? parameters, CancellationToken ct)
    {
        var health = await _api.GetHealthAsync(ct);
        return new JsonObject
        {
            ["api"] = health,
            ["gui"] = new JsonObject
            {
                ["connected"] = true,
                ["view"] = "Dashboard"
            }
        };
    }
}
