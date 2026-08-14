using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Tools;

namespace WupmMcp.Tools;

public sealed class ListTool : McpTool
{
    private readonly WupmApiClient _api;

    public ListTool(WupmApiClient api) : base("list", "List available or installed packages", new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["installed"] = new JsonObject { ["type"] = "boolean", ["description"] = "List installed packages instead of available" }
        }
    })
    {
        _api = api;
    }

    public override async Task<JsonNode> ExecuteAsync(JsonNode? parameters, CancellationToken ct)
    {
        var installed = parameters?["installed"] is JsonNode installedNode && bool.TryParse(installedNode.ToString(), out var installedBool) && installedBool;
        return installed ? await _api.ListInstalledAsync(ct) : await _api.ListPackagesAsync(ct);
    }
}
