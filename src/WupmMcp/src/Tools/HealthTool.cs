using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Tools;

namespace WupmMcp.Tools;

public sealed class HealthTool : McpTool
{
    private readonly WupmApiClient _api;

    public HealthTool(WupmApiClient api) : base("health", "Get WUPM API health status", new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() })
    {
        _api = api;
    }

    public override async Task<JsonNode> ExecuteAsync(JsonNode? parameters, CancellationToken ct)
    {
        return await _api.GetHealthAsync(ct);
    }
}
