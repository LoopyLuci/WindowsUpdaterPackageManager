using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WindowsUpdateAndPackageManager.Commands;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(Composition.Build(builder.Environment.ContentRootPath));

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { status = "ok", name = "wupm-api" }));

app.MapGet("/packages", async (IRepoSync repoSync, string repositoryUrl = "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager") =>
{
    var packages = await repoSync.ListAsync(repositoryUrl);
    return Results.Ok(packages);
});

app.MapGet("/installed", async (IPackageManager packageManager) =>
{
    var packages = await packageManager.ListInstalledAsync();
    return Results.Ok(packages);
});

app.MapPost("/install", async (IPackageManager packageManager, PackageManifest package) =>
{
    var result = await packageManager.InstallAsync(package);
    return Results.Ok(result);
});

app.MapPost("/sync", async (IRepoSync repoSync, string repositoryUrl = "https://github.com/LoopyLuci/WindowsUpdateAndPackageManager") =>
{
    var result = await repoSync.SyncAsync(repositoryUrl);
    return Results.Ok(result);
});

app.MapPost("/windows-update", async (IWindowsUpdateManager windowsUpdateManager) =>
{
    var result = await windowsUpdateManager.ScanAndInstallAsync();
    return Results.Ok(result);
});

app.MapGet("/audit", async (IAuditor auditor, DateTimeOffset? from = null, DateTimeOffset? to = null, string? action = null) =>
{
    var entries = await auditor.QueryAsync(from, to, action);
    return Results.Ok(entries);
});

app.Run();
