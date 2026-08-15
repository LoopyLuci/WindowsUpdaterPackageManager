using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Tools;

namespace WupmMcp.Tools;

public sealed class MarketplaceSearchTool : McpTool
{
    private readonly WupmApiClient _api;

    public MarketplaceSearchTool(WupmApiClient api) : base("marketplace_search", "Search marketplace plugins", new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["query"] = new JsonObject { ["type"] = "string", ["description"] = "Search query" }
        }
    })
    {
        _api = api;
    }

    public override async Task<JsonNode> ExecuteAsync(JsonNode? parameters, CancellationToken ct)
    {
        var query = parameters?["query"]?.ToString() ?? string.Empty;
        return await _api.MarketplaceSearchAsync(query, ct);
    }
}
