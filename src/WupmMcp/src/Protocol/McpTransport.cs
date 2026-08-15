using System.Text;
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
        using var stdin = Console.OpenStandardInput();
        using var reader = new StreamReader(stdin, Encoding.UTF8, leaveOpen: true);

        // Read headers until blank line
        string? line;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (!string.IsNullOrWhiteSpace(line = await reader.ReadLineAsync().ConfigureAwait(false)))
        {
            var idx = line.IndexOf(':', StringComparison.Ordinal);
            if (idx > 0)
            {
                headers[line[..idx].Trim()] = line[(idx + 1)..].Trim();
            }
        }

        if (!headers.TryGetValue("Content-Length", out var lengthText) || !int.TryParse(lengthText, out var length))
        {
            return null;
        }

        var buffer = new char[length];
        var read = 0;
        while (read < length)
        {
            var n = await reader.ReadAsync(buffer, read, length - read).ConfigureAwait(false);
            if (n == 0) return null;
            read += n;
        }

        var json = new string(buffer);
        return JsonNode.Parse(json);
    }

    public override async Task WriteAsync(JsonNode message, CancellationToken ct)
    {
        var json = message.ToJsonString();
        var payload = Encoding.UTF8.GetBytes(json);
        var header = $"Content-Length: {payload.Length}\r\n\r\n";

        await Console.Out.WriteAsync(header).ConfigureAwait(false);
        await Console.OpenStandardOutput().WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
        await Console.Out.FlushAsync().ConfigureAwait(false);
    }
}
