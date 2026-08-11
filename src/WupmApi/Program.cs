using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WindowsUpdateAndPackageManager.Commands;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Models;

var builder = WebApplication.CreateBuilder(args);
Composition.RegisterInto(builder.Services, builder.Environment.ContentRootPath);

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { status = "ok", name = "wupm-api" }));

app.MapGet("/packages", async (IServiceProvider sp, string repositoryUrl = "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager") =>
{
    var repoSync = sp.GetRequiredService<IRepoSync>();
    var packages = await repoSync.ListAsync(repositoryUrl);
    return Results.Ok(packages);
});

app.MapGet("/installed", async (IServiceProvider sp) =>
{
    var packageManager = sp.GetRequiredService<IPackageManager>();
    var packages = await packageManager.ListInstalledAsync();
    return Results.Ok(packages);
});

app.MapPost("/install", async (IServiceProvider sp, HttpRequest request) =>
{
    var packageManager = sp.GetRequiredService<IPackageManager>();
    var package = await JsonSerializer.DeserializeAsync<PackageManifest>(request.Body);
    if (package is null)
    {
        return Results.BadRequest(new { error = "Invalid package manifest." });
    }

    var result = await packageManager.InstallAsync(package);
    return Results.Ok(result);
});

app.MapPost("/sync", async (IServiceProvider sp, string repositoryUrl = "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager") =>
{
    var repoSync = sp.GetRequiredService<IRepoSync>();
    var result = await repoSync.SyncAsync(repositoryUrl);
    return Results.Ok(result);
});

app.MapPost("/windows-update", async (IServiceProvider sp) =>
{
    var manager = sp.GetRequiredService<IWindowsUpdateManager>();
    var result = await manager.ScanAndInstallAsync();
    return Results.Ok(result);
});

app.MapGet("/audit", async (IServiceProvider sp, DateTimeOffset? from = null, DateTimeOffset? to = null, string? action = null) =>
{
    var auditor = sp.GetRequiredService<IAuditor>();
    var entries = await auditor.QueryAsync(from, to, action);
    return Results.Ok(entries);
});

app.Run();
