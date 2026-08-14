using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Protocol;
using WupmMcp.Tools;

namespace WupmMcp;

public abstract class McpTool
{
    public string Name { get; }
    public string Description { get; }
    public JsonNode InputSchema { get; }

    protected McpTool(string name, string description, JsonNode inputSchema)
    {
        Name = name;
        Description = description;
        InputSchema = inputSchema;
    }

    public abstract Task<JsonNode> ExecuteAsync(JsonNode? parameters, CancellationToken ct);
}
