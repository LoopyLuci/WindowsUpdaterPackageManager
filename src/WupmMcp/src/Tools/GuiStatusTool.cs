using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Protocol;

namespace WupmMcp.Tools;

public sealed class GuiStatusTool : McpTool
{
    private readonly HttpClient _http;
    private const string GuiBase = "http://127.0.0.1:5003/gui";

    public GuiStatusTool() : base("gui_status", "Get GUI and service status", new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject()
    })
    {
        _http = new HttpClient();
    }

    public override async Task<JsonNode> ExecuteAsync(JsonNode? parameters, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, GuiBase);
        req.Content = new StringContent(new JsonObject { ["command"] = "status" }.ToJsonString(), Encoding.UTF8, "application/json");
        using var res = await _http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        return JsonNode.Parse(json)!;
    }
}
