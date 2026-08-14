using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Tools;

namespace WupmMcp.Tools;

public sealed class InstallTool : McpTool
{
    private readonly WupmApiClient _api;

    public InstallTool(WupmApiClient api) : base("install", "Install a package", new JsonObject
    {
        ["type"] = "object",
        ["required"] = new JsonArray("manifest"),
        ["properties"] = new JsonObject
        {
            ["manifest"] = new JsonObject { ["type"] = "object", ["description"] = "Package manifest JSON" }
        }
    })
    {
        _api = api;
    }

    public override async Task<JsonNode> ExecuteAsync(JsonNode? parameters, CancellationToken ct)
    {
        var manifest = parameters?["manifest"];
        if (manifest is null) throw new InvalidOperationException("manifest is required");
        return await _api.InstallAsync(manifest, ct);
    }
}
