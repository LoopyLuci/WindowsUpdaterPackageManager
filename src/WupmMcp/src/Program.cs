using System.Text.Json;
using System.Text.Json.Nodes;
using WupmMcp.Protocol;
using WupmMcp.Tools;

namespace WupmMcp;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.InputEncoding = System.Text.Encoding.UTF8;
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var transport = new StdioTransport();
        var api = new WupmApiClient();
        var tools = new McpToolRegistry(api);
        var session = new McpSession(transport, tools);

        await session.RunAsync();

        return 0;
    }
}
