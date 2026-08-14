using System.Text.Json;
using System.Text.Json.Nodes;

namespace WupmMcp.Protocol;

public sealed class McpRequest
{
    public string JsonRpc { get; set; } = "2.0";
    public string? Id { get; set; }
    public string? Method { get; set; }
    public JsonNode? Params { get; set; }
}

public sealed class McpResponse
{
    public string JsonRpc { get; set; } = "2.0";
    public string? Id { get; set; }
    public JsonNode? Result { get; set; }
    public McpError? Error { get; set; }

    public static McpResponse Ok(string? id, JsonNode? result) => new() { Id = id, Result = result };
    public static McpResponse Fail(string? id, int code, string message) => new() { Id = id, Error = new McpError { Code = code, Message = message } };
}

public sealed class McpError
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
}
