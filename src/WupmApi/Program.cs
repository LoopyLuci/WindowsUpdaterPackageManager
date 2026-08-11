using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WindowsUpdateAndPackageManager.Commands;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Models;

var logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/wupm-api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

Log.Logger = logger;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();
Composition.RegisterInto(builder.Services, builder.Environment.ContentRootPath);

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { status = "ok", name = "wupm-api" }));

app.MapGet("/packages", async (IServiceProvider sp, string repositoryUrl = "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager") =>
{
    var repoSync = sp.GetRequiredService<IRepoSync>();
    var packages = await repoSync.ListAsync(repositoryUrl);
    logger.LogInformation("Listed {Count} packages", packages.Count);
    return Results.Ok(packages);
});

app.MapGet("/installed", async (IServiceProvider sp) =>
{
    var packageManager = sp.GetRequiredService<IPackageManager>();
    var packages = await packageManager.ListInstalledAsync();
    logger.LogInformation("Listed {Count} installed packages", packages.Count);
    return Results.Ok(packages);
});

app.MapPost("/install", async (IServiceProvider sp, HttpRequest request) =>
{
    var packageManager = sp.GetRequiredService<IPackageManager>();
    var package = await JsonSerializer.DeserializeAsync<PackageManifest>(request.Body);
    if (package is null)
    {
        logger.LogWarning("Install request failed: invalid manifest");
        return Results.BadRequest(new { error = "Invalid package manifest." });
    }

    var result = await packageManager.InstallAsync(package);
    logger.LogInformation("Install {PackageId} success={Success}", package.Id, result.Success);
    return Results.Ok(result);
});

app.MapPost("/sync", async (IServiceProvider sp, string repositoryUrl = "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager") =>
{
    var repoSync = sp.GetRequiredService<IRepoSync>();
    var result = await repoSync.SyncAsync(repositoryUrl);
    logger.LogInformation("Sync success={Success} message={Message}", result.Success, result.Message);
    return Results.Ok(result);
});

app.MapPost("/windows-update", async (IServiceProvider sp) =>
{
    var manager = sp.GetRequiredService<IWindowsUpdateManager>();
    var result = await manager.ScanAndInstallAsync();
    logger.LogInformation("Windows update result success={Success} message={Message}", result.Success, result.Message);
    return Results.Ok(result);
});

app.MapGet("/audit", async (IServiceProvider sp, DateTimeOffset? from = null, DateTimeOffset? to = null, string? action = null) =>
{
    var auditor = sp.GetRequiredService<IAuditor>();
    var entries = await auditor.QueryAsync(from, to, action);
    logger.LogInformation("Audit query returned {Count} entries", entries.Count);
    return Results.Ok(entries);
});

var apiKey = Environment.GetEnvironmentVariable("WUPM_API_KEY");
if (!string.IsNullOrWhiteSpace(apiKey))
{
    app.Use(async (context, next) =>
    {
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
            logger.LogWarning("Unauthorized request to {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
            return;
        }

        await next();
    });
}

app.Run();
