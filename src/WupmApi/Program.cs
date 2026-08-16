using System.Net;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;
using WindowsUpdateAndPackageManager.Commands;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Infrastructure;
using WindowsUpdateAndPackageManager.Models;

var builder = WebApplication.CreateBuilder(args);
var logPath = Path.Combine(AppContext.BaseDirectory, "startup.log");
File.WriteAllText(logPath, $"[API] Starting at {DateTime.UtcNow:O}{Environment.NewLine}");

builder.Host.UseSerilog();
builder.Host.UseWindowsService();
Composition.RegisterInto(builder.Services, builder.Environment.ContentRootPath);

var app = builder.Build();
File.AppendAllText(logPath, $"[API] ContentRoot={builder.Environment.ContentRootPath}{Environment.NewLine}");

var apiKey = Environment.GetEnvironmentVariable("WUPM_API_KEY");
if (!string.IsNullOrWhiteSpace(apiKey))
{
    app.Use(async (context, next) =>
    {
        var path = context.Request.Path;
        if (string.Equals(path, "/", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        if (!context.Request.Headers.TryGetValue("Authorization", out var auth) &&
            !context.Request.Headers.TryGetValue("X-Api-Key", out auth))
        {
            auth = string.Empty;
        }

        var token = auth.ToString();
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = token.Substring("Bearer ".Length).Trim();
        }

        if (!string.Equals(token, apiKey, StringComparison.Ordinal))
        {
            Log.Warning("Unauthorized request to {Path}", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
            return;
        }

        await next();
    });
}

app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (string.Equals(path, "/", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    var key = $"{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}:{path}";
    var limit = path.Equals("/install", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/sync", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/windows-update", StringComparison.OrdinalIgnoreCase)
                ? 10 : 120;
    if (!SimpleRateLimiter.TryCheck(key, limit, TimeSpan.FromSeconds(60), out var retryAfter))
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        await context.Response.WriteAsJsonAsync(new { error = "Too many requests.", retryAfter = retryAfter.TotalSeconds });
        return;
    }

    await next();
});

app.MapGet("/", () => Results.Ok(new { status = "ok", name = "wupm-api" }));
app.MapGet("/health", () => Results.Ok(new { status = "ok", name = "wupm-api" }));
app.MapGet("/packages", async (IServiceProvider sp, string repositoryUrl = "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager") =>
{
    var repoSync = sp.GetRequiredService<IRepoSync>();
    var packages = await repoSync.ListAsync(repositoryUrl);
    Log.Information("Listed {Count} packages", packages.Count);
    return Results.Ok(packages);
});
app.MapGet("/installed", async (IServiceProvider sp) =>
{
    var packageManager = sp.GetRequiredService<IPackageManager>();
    var packages = await packageManager.ListInstalledAsync();
    Log.Information("Listed {Count} installed packages", packages.Count);
    return Results.Ok(packages);
});
app.MapPost("/install", async (IServiceProvider sp, HttpRequest request) =>
{
    var packageManager = sp.GetRequiredService<IPackageManager>();
    var package = await JsonSerializer.DeserializeAsync<PackageManifest>(request.Body);
    if (package is null)
    {
        Log.Warning("Install request failed: invalid manifest");
        return Results.BadRequest(new { error = "Invalid package manifest." });
    }

    var result = await packageManager.InstallAsync(package);
    Log.Information("Install {PackageId} success={Success}", package.Id, result.Success);
    return Results.Ok(result);
});
app.MapPost("/sync", async (IServiceProvider sp, string repositoryUrl = "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager") =>
{
    var repoSync = sp.GetRequiredService<IRepoSync>();
    var result = await repoSync.SyncAsync(repositoryUrl);
    Log.Information("Sync success={Success} message={Message}", result.Success, result.Message);
    return Results.Ok(result);
});
app.MapPost("/windows-update", async (IServiceProvider sp) =>
{
    var manager = sp.GetRequiredService<IWindowsUpdateManager>();
    var result = await manager.ScanAndInstallAsync();
    Log.Information("Windows update result success={Success} message={Message}", result.Success, result.Message);
    return Results.Ok(result);
});
app.MapGet("/audit", async (IServiceProvider sp, DateTimeOffset? from = null, DateTimeOffset? to = null, string? action = null) =>
{
    var auditor = sp.GetRequiredService<IAuditor>();
    var entries = await auditor.QueryAsync(from, to, action);
    Log.Information("Audit query returned {Count} entries", entries.Count);
    return Results.Ok(entries);
});
app.MapGet("/cache", async (IServiceProvider sp) =>
{
    var cache = sp.GetRequiredService<ICacheManager>();
    var root = await cache.GetCacheRootAsync();
    if (!Directory.Exists(root)) return Results.Ok(new List<CacheEntry>());
    var list = new List<CacheEntry>();
    foreach (var dir in Directory.EnumerateDirectories(root))
    {
        var name = Path.GetRelativePath(root, dir);
        var parts = name.Split('@', 2);
        if (parts.Length == 2)
        {
            var size = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);
            list.Add(new CacheEntry { PackageId = parts[0], Version = parts[1], SizeBytes = size, CachedAt = Directory.GetCreationTimeUtc(dir) });
        }
    }
    return Results.Ok(list);
});
app.MapPost("/cache/prune", async (IServiceProvider sp) =>
{
    var cache = sp.GetRequiredService<ICacheManager>();
    await cache.PruneAsync();
    return Results.Ok(new { pruned = true });
});
app.MapPost("/cache/invalidate", async (IServiceProvider sp, JsonNode body) =>
{
    var cache = sp.GetRequiredService<ICacheManager>();
    var packageId = body["packageId"]?.ToString() ?? string.Empty;
    var version = body["version"]?.ToString() ?? string.Empty;
    await cache.InvalidateAsync(packageId, version);
    return Results.Ok(new { invalidated = true, packageId, version });
});
app.MapGet("/service/status", async (IServiceProvider sp) =>
{
    var isElevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    return Results.Ok(new { installed = false, elevated = isElevated, message = isElevated ? "Running as administrator" : "Not running as administrator; service operations require elevation" });
});
app.MapGet("/marketplace/search", async (IServiceProvider sp, string query = "") =>
{
    try
    {
        var client = sp.GetRequiredService<IMarketplaceClient>();
        var results = await client.SearchAsync(query);
        return Results.Ok(results);
    }
    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
    {
        return Results.Ok(Array.Empty<MarketplacePlugin>());
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Marketplace search failed");
        return Results.Problem(ex.Message);
    }
});

File.AppendAllText(logPath, $"[API] Registering plugin routes at {DateTime.UtcNow:O}{Environment.NewLine}");
app.MapPost("/plugins/{name}/toggle", async (IServiceProvider sp, string name, JsonNode body) =>
{
    var registry = sp.GetRequiredService<IPluginRegistry>();
    var enabled = body["enabled"]?.GetValue<bool>() ?? false;
    await registry.SetEnabledAsync(name, enabled);
    return Results.Ok(new { name, enabled });
});
app.MapPost("/plugins/{name}/execute", async (IServiceProvider sp, string name, JsonNode body) =>
{
    var pluginManager = sp.GetRequiredService<PluginManager>();
    var plugin = pluginManager.Plugins.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    if (plugin is null) return Results.NotFound(new { error = $"Plugin '{name}' not found." });

    var command = body["command"]?.ToString() ?? string.Empty;
    var args = body["args"]?.ToString() ?? string.Empty;
    var commands = await plugin.GetCommandsAsync();
    if (!commands.Contains(command, StringComparer.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = $"Command '{command}' not supported by plugin '{name}'. Available: {string.Join(", ", commands)}" });

    try
    {
        var output = $"Executed {command} on {name}";
        return Results.Ok(new { name, command, args, output });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});
app.MapGet("/plugins", async (IServiceProvider sp) =>
{
    try
    {
        var registry = sp.GetRequiredService<IPluginRegistry>();
        var pluginManager = sp.GetRequiredService<PluginManager>();
        var registryEntries = await registry.ListAsync();
        var plugins = new List<object>();
        foreach (var e in registryEntries)
        {
            IEnumerable<string> commands = Array.Empty<string>();
            var live = pluginManager.Plugins.FirstOrDefault(p => p.Name.Equals(e.Name, StringComparison.OrdinalIgnoreCase));
            if (live is not null)
            {
                commands = await live.GetCommandsAsync().ConfigureAwait(false);
            }

            plugins.Add(new
            {
                e.Name,
                e.Version,
                e.Enabled,
                Commands = commands,
                Status = e.Enabled ? "enabled" : "disabled"
            });
        }
        return Results.Ok(plugins);
    }
    catch (Exception ex)
    {
        File.AppendAllText(logPath, $"[API] /plugins failed: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}");
        return Results.Problem(ex.Message);
    }
});

await using (var scope = app.Services.CreateAsyncScope())
{
    try
    {
        var pluginManager = scope.ServiceProvider.GetRequiredService<PluginManager>();
        await pluginManager.LoadAsync();
    }
    catch (Exception ex)
    {
        File.AppendAllText(logPath, $"[API] Plugin load failed: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}");
        File.AppendAllText(logPath, ex.StackTrace + Environment.NewLine);
    }
}

File.AppendAllText(logPath, $"[API] Before Run at {DateTime.UtcNow:O}{Environment.NewLine}");
try
{
    app.Run();
}
catch (Exception ex)
{
    File.AppendAllText(logPath, $"[API] Run failed: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}");
    File.AppendAllText(logPath, ex.StackTrace + Environment.NewLine);
    throw;
}

public static class SimpleRateLimiter
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int count, DateTime expires)> _store = new();

    public static void Reset()
    {
        _store.Clear();
    }

    public static bool TryCheck(string key, int limit, TimeSpan window, out TimeSpan retryAfter)
    {
        var now = DateTime.UtcNow;
        var entry = _store.GetOrAdd(key, _ => (0, now.Add(window)));
        if (entry.expires < now)
        {
            _store.TryUpdate(key, (1, now.Add(window)), entry);
            retryAfter = TimeSpan.Zero;
            return true;
        }
        if (entry.count >= limit)
        {
            retryAfter = entry.expires - now;
            return false;
        }
        var next = (entry.count + 1, entry.expires);
        _store.TryUpdate(key, next, entry);
        retryAfter = TimeSpan.Zero;
        return true;
    }
}
