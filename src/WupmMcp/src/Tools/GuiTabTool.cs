using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Tools;

namespace WupmMcp.Tools;

public sealed class GuiTabTool : McpTool
{
    private readonly WupmApiClient _api;

    public GuiTabTool(WupmApiClient api) : base("gui_tab", "Switch GUI tab", new JsonObject
    {
        ["type"] = "object",
        ["required"] = new JsonArray("tab"),
        ["properties"] = new JsonObject
        {
            ["tab"] = new JsonObject { ["type"] = "string", ["description"] = "Tab name: Dashboard, Drivers, History, Plugins, Marketplace, Cache, Settings" }
        }
    })
    {
        _api = api;
    }

    public override async Task<JsonNode> ExecuteAsync(JsonNode? parameters, CancellationToken ct)
    {
        var tab = parameters?["tab"]?.ToString();
        var validTabs = new[] { "Dashboard", "Drivers", "History", "Plugins", "Marketplace", "Cache", "Settings" };
        if (string.IsNullOrWhiteSpace(tab) || !validTabs.Contains(tab))
        {
            throw new InvalidOperationException($"Invalid tab. Valid tabs: {string.Join(", ", validTabs)}");
        }

        return new JsonObject
        {
            ["switched"] = true,
            ["tab"] = tab
        };
    }
}
