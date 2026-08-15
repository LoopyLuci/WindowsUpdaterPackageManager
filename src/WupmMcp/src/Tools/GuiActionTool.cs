using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Protocol;

namespace WupmMcp.Tools;

public sealed class GuiActionTool : McpTool
{
    private readonly HttpClient _http;
    private const string GuiBase = "http://127.0.0.1:5001/gui";

    public GuiActionTool() : base("gui_action", "Trigger a GUI action", new JsonObject
    {
        ["type"] = "object",
        ["required"] = new JsonArray("action"),
        ["properties"] = new JsonObject
        {
            ["action"] = new JsonObject { ["type"] = "string", ["description"] = "Action: scan, install, prune, cancel" },
            ["params"] = new JsonObject { ["type"] = "object", ["description"] = "Optional action parameters" }
        }
    })
    {
        _http = new HttpClient();
    }

    public override async Task<JsonNode> ExecuteAsync(JsonNode? parameters, CancellationToken ct)
    {
        var payload = new JsonObject
        {
            ["command"] = "action",
            ["action"] = parameters?["action"]?.ToString() ?? string.Empty,
            ["params"] = parameters?["params"] ?? new JsonObject()
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, GuiBase);
        req.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using var res = await _http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        return JsonNode.Parse(json)!;
    }
}
