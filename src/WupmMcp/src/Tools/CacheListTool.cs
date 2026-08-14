using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Tools;

namespace WupmMcp.Tools;

public sealed class CacheListTool : McpTool
{
    private readonly WupmApiClient _api;

    public CacheListTool(WupmApiClient api) : base("cache_list", "List cached packages", new JsonObject
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
