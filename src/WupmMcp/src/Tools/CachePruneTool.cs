using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Tools;

namespace WupmMcp.Tools;

public sealed class CachePruneTool : McpTool
{
    private readonly WupmApiClient _api;

    public CachePruneTool(WupmApiClient api) : base("cache_prune", "Prune cached packages", new JsonObject
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
