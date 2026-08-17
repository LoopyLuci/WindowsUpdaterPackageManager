using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Protocol;

namespace WupmMcp.Tools;

public sealed class UpdatesListTool : McpTool
{
    private readonly HttpClient _http;
    private const string ApiBase = "http://127.0.0.1:5002";

    public UpdatesListTool() : base("updates_list", "List available updates for this Windows version", new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["windowsVersion"] = new JsonObject { ["type"] = "string" },
            ["channel"] = new JsonObject { ["type"] = "string" }
        }
    })
    {
        _http = new HttpClient();
    }

    public override async Task<JsonNode> ExecuteAsync(JsonNode? parameters, CancellationToken ct)
    {
        var windowsVersion = parameters?["windowsVersion"]?.GetValue<string>();
        var channel = parameters?["channel"]?.GetValue<string>();
        var payload = new JsonObject
        {
            ["command"] = "updates",
            ["arguments"] = new JsonObject
            {
                ["for"] = windowsVersion,
                ["channel"] = channel
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/cli/execute");
        req.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using var res = await _http.SendAsync(req, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        return JsonNode.Parse(json) ?? new JsonObject();
    }
}
