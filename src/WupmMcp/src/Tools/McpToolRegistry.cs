using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Protocol;

namespace WupmMcp.Tools;

public sealed class McpToolRegistry
{
    private readonly WupmApiClient _api;
    private readonly Dictionary<string, McpTool> _tools;

    public McpToolRegistry(WupmApiClient api)
    {
        _api = api;
        _tools = new Dictionary<string, McpTool>(StringComparer.OrdinalIgnoreCase)
        {
            ["health"] = new HealthTool(_api),
            ["scan"] = new ScanTool(_api),
            ["install"] = new InstallTool(_api),
            ["list"] = new ListTool(_api),
            ["cache_list"] = new CacheListTool(_api),
            ["cache_prune"] = new CachePruneTool(_api),
            ["plugins_list"] = new PluginsListTool(_api),
            ["plugins_execute"] = new PluginsExecuteTool(_api),
            ["marketplace_search"] = new MarketplaceSearchTool(_api),
            ["gui_status"] = new GuiStatusTool(),
            ["gui_tab"] = new GuiTabTool(),
            ["gui_action"] = new GuiActionTool()
        };
    }

    public IReadOnlyDictionary<string, McpTool> All => _tools;

    public JsonNode ListTools()
    {
        var arr = new JsonArray();
        foreach (var tool in _tools.Values)
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

    public async Task<JsonNode> InvokeAsync(string name, JsonNode? parameters, CancellationToken ct)
    {
        if (!_tools.TryGetValue(name, out var tool))
        {
            throw new InvalidOperationException($"Unknown tool: {name}");
        }

        return await tool.ExecuteAsync(parameters, ct);
    }
}
