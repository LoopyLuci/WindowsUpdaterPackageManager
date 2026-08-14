using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Tools;

namespace WupmMcp.Tools;

public sealed class ScanTool : McpTool
{
    private readonly WupmApiClient _api;

    public ScanTool(WupmApiClient api) : base("scan", "Scan for Windows updates", new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["offlineScan"] = new JsonObject { ["type"] = "boolean", ["description"] = "Enable offline scan" }
        }
    })
    {
        _api = api;
    }

    public override async Task<JsonNode> ExecuteAsync(JsonNode? parameters, CancellationToken ct)
    {
        var offline = parameters?["offlineScan"] is JsonNode node && bool.TryParse(node.ToString(), out var b) && b;
        return await _api.ScanAsync(offline, ct);
    }
}
