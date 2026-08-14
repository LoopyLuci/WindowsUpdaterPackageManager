using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Tools;

namespace WupmMcp.Tools;

public sealed class PluginsListTool : McpTool
{
    private readonly WupmApiClient _api;

    public PluginsListTool(WupmApiClient api) : base("plugins_list", "List installed plugins", new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject()
    })
    {
        _api = api;
    }

    public override async Task<JsonNode> ExecuteAsync(JsonNode? parameters, CancellationToken ct)
    {
        return await _api.GetHealthAsync(ct);
    }
}
