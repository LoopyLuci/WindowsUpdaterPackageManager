using System.Text.Json;
using System.Text.Json.Nodes;

namespace WupmMcp.Protocol;

public static class McpResponseExtensions
{
    public static JsonNode ToJsonNode(this McpResponse response)
    {
        var obj = new JsonObject
        {
            ["jsonrpc"] = response.JsonRpc,
            ["id"] = response.Id
        };

        if (response.Error is not null)
        {
            obj["error"] = JsonNode.Parse(JsonSerializer.Serialize(response.Error))!;
        }
        else
        {
            obj["result"] = response.Result;
        }

        return obj;
    }
}
