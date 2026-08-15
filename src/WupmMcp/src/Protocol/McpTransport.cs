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
        var headerText = await ReadHeadersAsync(stdin, ct).ConfigureAwait(false);
        if (headerText is null) return null;

        var lengthText = GetHeaderValue(headerText, "Content-Length");
        if (!int.TryParse(lengthText, out var length) || length <= 0)
        {
            return null;
        }

        var body = new byte[length];
        var read = 0;
        while (read < length)
        {
            var n = await stdin.ReadAsync(body, read, length - read, ct).ConfigureAwait(false);
            if (n == 0) return null;
            read += n;
        }

        var json = Encoding.UTF8.GetString(body);
        return JsonNode.Parse(json);
    }

    private static async Task<string?> ReadHeadersAsync(Stream stdin, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var buffer = new byte[1];
        var line = new List<byte>();

        while (true)
        {
            var read = await stdin.ReadAsync(buffer, 0, 1, ct).ConfigureAwait(false);
            if (read == 0) return null;

            var b = buffer[0];
            if (b == '\n')
            {
                var text = Encoding.UTF8.GetString([.. line]).TrimEnd('\r');
                line.Clear();

                if (string.IsNullOrWhiteSpace(text))
                {
                    return sb.ToString();
                }

                sb.AppendLine(text);
            }
            else
            {
                line.Add(b);
            }
        }
    }

    private static string? GetHeaderValue(string headerBlock, string key)
    {
        foreach (var line in headerBlock.Split('\n'))
        {
            var idx = line.IndexOf(':', StringComparison.Ordinal);
            if (idx > 0 && line[..idx].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return line[(idx + 1)..].Trim();
            }
        }
        return null;
    }

    public override async Task WriteAsync(JsonNode message, CancellationToken ct)
    {
        var json = message.ToJsonString();
        var payload = Encoding.UTF8.GetBytes(json);
        var header = $"Content-Length: {payload.Length}\r\n\r\n";
        var frame = new byte[header.Length + payload.Length];
        Buffer.BlockCopy(Encoding.UTF8.GetBytes(header), 0, frame, 0, header.Length);
        Buffer.BlockCopy(payload, 0, frame, header.Length, payload.Length);

        await Console.OpenStandardOutput().WriteAsync(frame, 0, frame.Length, ct).ConfigureAwait(false);
        await Console.Out.FlushAsync().ConfigureAwait(false);
    }
}
