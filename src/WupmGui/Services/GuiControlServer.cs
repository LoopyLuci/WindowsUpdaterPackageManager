using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using WupmGui.ViewModels;
using WupmGui.Views;

namespace WupmGui.Services;

public sealed class GuiControlServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly MainWindow _window;
    private readonly MainWindowViewModel _vm;
    private bool _disposed;

    public GuiControlServer(MainWindow window, MainWindowViewModel vm, int port = 5003)
    {
        _window = window;
        _vm = vm;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/gui/");
        _listener.Start();
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        while (!_disposed)
        {
            try
            {
                var ctx = await _listener.GetContextAsync();
                _ = HandleAsync(ctx);
            }
            catch (ObjectDisposedException) { break; }
            catch (HttpListenerException) { break; }
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            var req = ctx.Request;
            var res = ctx.Response;
            res.ContentType = "application/json";

            if (!req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(res, 405, new JsonObject { ["error"] = "method not allowed" });
                return;
            }

            using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();
            var obj = JsonNode.Parse(body) as JsonObject;

            var command = obj?["command"]?.ToString();
            switch (command)
            {
                case "tab":
                {
                    var tab = obj?["tab"]?.ToString();
                    var valid = new[] { "Dashboard", "Drivers", "History", "Plugins", "Marketplace", "Cache", "Settings" };
                    if (string.IsNullOrWhiteSpace(tab) || !valid.Contains(tab))
                    {
                        var err = new JsonObject { ["error"] = $"Invalid tab. Valid: {string.Join(", ", valid)}" };
                        await WriteJsonAsync(res, 400, err);
                        return;
                    }

                    _window.Dispatcher.Invoke(() =>
                    {
                        _vm.CurrentViewModel = tab.ToLowerInvariant() switch
                        {
                            "drivers" => _vm.Drivers,
                            "history" => _vm.History,
                            "plugins" => _vm.Plugins,
                            "marketplace" => _vm.Marketplace,
                            "cache" => _vm.Cache,
                            "settings" => _vm.Settings,
                            _ => _vm.Dashboard
                        };
                    });

                    var tabResult = new JsonObject { ["switched"] = true, ["tab"] = tab };
                    await WriteJsonAsync(res, 200, tabResult);
                    return;
                }
                case "action":
                {
                    var action = obj?["action"]?.ToString();
                    var actionResult = new JsonObject { ["executed"] = true, ["action"] = action };
                    await WriteJsonAsync(res, 200, actionResult);
                    return;
                }
                case "status":
                {
                    var statusResult = new JsonObject
                    {
                        ["view"] = _vm.CurrentViewModel?.GetType().Name ?? "Unknown",
                        ["status"] = _vm.Status,
                        ["connected"] = _vm.IsConnected
                    };
                    await WriteJsonAsync(res, 200, statusResult);
                    return;
                }
                default:
                {
                    var unknown = new JsonObject { ["error"] = $"Unknown command: {command}" };
                    await WriteJsonAsync(res, 400, unknown);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            try { await WriteJsonAsync(ctx.Response, 500, new JsonObject { ["error"] = ex.Message }); } catch { }
        }
    }

    private async Task WriteJsonAsync(HttpListenerResponse res, int statusCode, JsonNode payload)
    {
        res.StatusCode = statusCode;
        var json = payload.ToJsonString();
        var bytes = Encoding.UTF8.GetBytes(json);
        res.ContentLength64 = bytes.Length;
        await res.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        res.OutputStream.Close();
    }

    public void Dispose()
    {
        _disposed = true;
        _listener.Stop();
        _listener.Close();
        GC.SuppressFinalize(this);
    }
}
