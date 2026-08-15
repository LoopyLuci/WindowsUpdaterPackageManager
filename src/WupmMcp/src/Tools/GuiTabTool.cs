using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Protocol;

namespace WupmMcp.Tools;

public sealed class GuiTabTool : McpTool
{
    private readonly HttpClient _http;
    private const string GuiBase = "http://127.0.0.1:5003/gui";

    public GuiTabTool() : base("gui_tab", "Switch GUI tab", new JsonObject
    {
        ["type"] = "object",
        ["required"] = new JsonArray("tab"),
        ["properties"] = new JsonObject
        {
            ["tab"] = new JsonObject { ["type"] = "string", ["description"] = "Tab name: Dashboard, Drivers, History, Plugins, Marketplace, Cache, Settings" }
        }
    })
    {
        _http = new HttpClient();
    }

    public override async Task<JsonNode> ExecuteAsync(JsonNode? parameters, CancellationToken ct)
    {
        var payload = new JsonObject
        {
            ["command"] = "tab",
            ["tab"] = parameters?["tab"]?.ToString() ?? string.Empty
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, GuiBase);
        req.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync(ct);
        return JsonNode.Parse(json)!;
    }
}
