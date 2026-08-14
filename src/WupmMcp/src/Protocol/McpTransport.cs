using System.Text.Json;
using System.Text.Json.Nodes;

namespace WupmMcp.Protocol;

public abstract class McpTransport
{
    public abstract Task<JsonNode?> ReadAsync(CancellationToken ct);
    public abstract Task WriteAsync(JsonNode message, CancellationToken ct);
}

public sealed class StdioTransport : McpTransport
{
    public override async Task<JsonNode?> ReadAsync(CancellationToken ct)
    {
        using var reader = new StreamReader(Console.OpenStandardInput(), leaveOpen: true);
        var line = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(line)) return null;
        return JsonNode.Parse(line);
    }

    public override async Task WriteAsync(JsonNode message, CancellationToken ct)
    {
        var json = message.ToJsonString();
        await Console.Out.WriteLineAsync(json);
        await Console.Out.FlushAsync();
    }
}
