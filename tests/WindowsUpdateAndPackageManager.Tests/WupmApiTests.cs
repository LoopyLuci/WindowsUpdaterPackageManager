using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WindowsUpdateAndPackageManager.Core;
using WindowsUpdateAndPackageManager.Data;
using WindowsUpdateAndPackageManager.Infrastructure;
using WindowsUpdateAndPackageManager.Models;
using Xunit;

namespace WindowsUpdateAndPackageManager.Tests;

public sealed class WupmApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WupmApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var repoMock = new Mock<IRepoSync>();
                repoMock.Setup(x => x.ListAsync(It.IsAny<string>(), default)).ReturnsAsync(new[] { new PackageManifest { Id = "test", Version = "1.0" } });
                repoMock.Setup(x => x.SyncAsync(It.IsAny<string>(), default)).ReturnsAsync(new SyncResult { Success = true });
                services.Remove(services.SingleOrDefault(d => d.ServiceType == typeof(IRepoSync))!);
                services.AddSingleton(repoMock.Object);

                var pmMock = new Mock<IPackageManager>();
                pmMock.Setup(x => x.ListInstalledAsync(default)).ReturnsAsync(new[] { new PackageManifest { Id = "installed", Version = "1.0" } });
                pmMock.Setup(x => x.InstallAsync(It.IsAny<PackageManifest>(), default)).ReturnsAsync(new InstallResult { PackageId = "test", Success = true });
                services.Remove(services.SingleOrDefault(d => d.ServiceType == typeof(IPackageManager))!);
                services.AddSingleton(pmMock.Object);

                var wuMock = new Mock<IWindowsUpdateManager>();
                wuMock.Setup(x => x.ScanAndInstallAsync(It.IsAny<bool>(), default)).ReturnsAsync(new WindowsUpdateResult { Success = true });
                services.Remove(services.SingleOrDefault(d => d.ServiceType == typeof(IWindowsUpdateManager))!);
                services.AddSingleton(wuMock.Object);

                var auditorMock = new Mock<IAuditor>();
                auditorMock.Setup(x => x.QueryAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<string>(), default)).ReturnsAsync(new List<AuditEntry> { new AuditEntry { Action = "test" } });
                services.Remove(services.SingleOrDefault(d => d.ServiceType == typeof(IAuditor))!);
                services.AddSingleton(auditorMock.Object);
            });
        });
    }

    [Fact]
    public async Task Root_returns_ok_status()
    {
        SimpleRateLimiter.Reset();
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Packages_endpoint_returns_mocked_packages()
    {
        SimpleRateLimiter.Reset();
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/packages?repositoryUrl=https://example.invalid/repo");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Installed_endpoint_returns_mocked_installed()
    {
        SimpleRateLimiter.Reset();
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/installed");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Install_endpoint_returns_ok_with_mock()
    {
        SimpleRateLimiter.Reset();
        using var client = _factory.CreateClient();
        var payload = new StringContent("{\"id\":\"test\",\"version\":\"1.0\"}", System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/install", payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Sync_endpoint_returns_ok_with_mock()
    {
        SimpleRateLimiter.Reset();
        using var client = _factory.CreateClient();
        using var response = await client.PostAsync("/sync?repositoryUrl=https://example.invalid/repo", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WindowsUpdate_endpoint_returns_ok_with_mock()
    {
        SimpleRateLimiter.Reset();
        using var client = _factory.CreateClient();
        using var response = await client.PostAsync("/windows-update", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Audit_endpoint_returns_ok_with_mock()
    {
        SimpleRateLimiter.Reset();
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/audit");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task With_api_key_set_missing_header_returns_unauthorized()
    {
        try
        {
            Environment.SetEnvironmentVariable("WUPM_API_KEY", "secret");
            SimpleRateLimiter.Reset();
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            using var response = await client.GetAsync("/audit");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WUPM_API_KEY", null);
        }
    }

    [Fact]
    public async Task With_api_key_set_valid_bearer_returns_ok()
    {
        try
        {
            Environment.SetEnvironmentVariable("WUPM_API_KEY", "secret");
            SimpleRateLimiter.Reset();
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "secret");
            using var response = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WUPM_API_KEY", null);
        }
    }

    [Fact]
    public async Task With_api_key_set_valid_xapikey_returns_ok()
    {
        try
        {
            Environment.SetEnvironmentVariable("WUPM_API_KEY", "secret");
            SimpleRateLimiter.Reset();
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", "secret");
            using var response = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WUPM_API_KEY", null);
        }
    }

    [Fact]
    public async Task Install_endpoint_uses_stricter_rate_limit_than_listing()
    {
        const int limit = 10;
        try
        {
            Environment.SetEnvironmentVariable("WUPM_API_KEY", "secret");
            SimpleRateLimiter.Reset();
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Api-Key", "secret");
            var payload = new StringContent("{\"id\":\"test\",\"version\":\"1.0\"}", System.Text.Encoding.UTF8, "application/json");

            for (var i = 0; i < limit; i++)
            {
                using var response = await client.PostAsync("/install", payload);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            using var responseAfterLimit = await client.PostAsync("/install", payload);
            Assert.Equal(HttpStatusCode.TooManyRequests, responseAfterLimit.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WUPM_API_KEY", null);
            SimpleRateLimiter.Reset();
        }
    }

    [Fact]
    public async Task With_mtls_enabled_without_client_certificate_returns_forbidden()
    {
        try
        {
            Environment.SetEnvironmentVariable("WUPM_API_KEY", null);
            Environment.SetEnvironmentVariable("WUPM_API_MTLS_ENABLED", "true");
            Environment.SetEnvironmentVariable("WUPM_API_MTLS_ALLOWED_THUMBPRINTS", "AAA");
            SimpleRateLimiter.Reset();
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();
            using var response = await client.GetAsync("/installed");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WUPM_API_KEY", null);
            Environment.SetEnvironmentVariable("WUPM_API_MTLS_ENABLED", null);
            Environment.SetEnvironmentVariable("WUPM_API_MTLS_ALLOWED_THUMBPRINTS", null);
            SimpleRateLimiter.Reset();
        }
    }
}
