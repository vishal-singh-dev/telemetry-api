using System.Net;
using System.Net.Http.Json;
using Api.Configuration;
using Api.Endpoints;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.IntegrationTests;

public class TestEndpointsIntegrationTests
{
    private class ConfigurableTestFactory : CustomWebApplicationFactory
    {
        private readonly bool _enabled;
        private readonly bool _allowWrites;
        private readonly int _maxWrites;

        public ConfigurableTestFactory(bool enabled, bool allowWrites, int maxWrites = 5)
        {
            _enabled = enabled;
            _allowWrites = allowWrites;
            _maxWrites = maxWrites;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                // Disable rate limiting for test endpoint scenario assertions so rate limits don't interfere
                services.Configure<RateLimitingOptions>(opts =>
                {
                    opts.Enabled = false;
                });

                services.Configure<TestEndpointsOptions>(opts =>
                {
                    opts.Enabled = _enabled;
                    opts.AllowWrites = _allowWrites;
                    opts.MaxSyntheticWritesPerRun = _maxWrites;
                    opts.MaxRunDurationMinutes = 10;
                    opts.RequireTestRunId = true;
                });
            });
        }
    }

    [Fact]
    public async Task TestEndpoints_WhenDisabled_ReturnsForbidden()
    {
        using var factory = new ConfigurableTestFactory(enabled: false, allowWrites: false);
        var client = factory.CreateClient();

        var readRes = await client.GetAsync("/api/test/db-read");
        Assert.Equal(HttpStatusCode.Forbidden, readRes.StatusCode);

        var writeRes = await client.PostAsJsonAsync("/api/test/db-write", new SyntheticWriteRequest("run-1", "test", "payload"));
        Assert.Equal(HttpStatusCode.Forbidden, writeRes.StatusCode);

        var deleteRes = await client.DeleteAsync("/api/test/runs/run-1");
        Assert.Equal(HttpStatusCode.Forbidden, deleteRes.StatusCode);
    }

    [Fact]
    public async Task TestEndpoints_WhenEnabledButWritesDisallowed_RejectsWrites()
    {
        using var factory = new ConfigurableTestFactory(enabled: true, allowWrites: false);
        var client = factory.CreateClient();

        // Read works
        var readRes = await client.GetAsync("/api/test/db-read");
        Assert.Equal(HttpStatusCode.OK, readRes.StatusCode);

        // Write is forbidden
        var writeRes = await client.PostAsJsonAsync("/api/test/db-write", new SyntheticWriteRequest("run-1", "test", "payload"));
        Assert.Equal(HttpStatusCode.Forbidden, writeRes.StatusCode);
    }

    [Fact]
    public async Task TestEndpoints_WhenWritesAllowed_MissingRunId_ReturnsBadRequest()
    {
        using var factory = new ConfigurableTestFactory(enabled: true, allowWrites: true);
        var client = factory.CreateClient();

        var writeRes = await client.PostAsJsonAsync("/api/test/db-write", new SyntheticWriteRequest(null, "test", "payload"));
        Assert.Equal(HttpStatusCode.BadRequest, writeRes.StatusCode);
    }

    [Fact]
    public async Task TestEndpoints_WriteAndScopedCleanup_OperatesOnlyOnTargetRunId()
    {
        using var factory = new ConfigurableTestFactory(enabled: true, allowWrites: true, maxWrites: 10);
        var client = factory.CreateClient();

        // Write 2 records for run-A
        client.DefaultRequestHeaders.Remove("X-Test-Run-Id");
        client.DefaultRequestHeaders.Add("X-Test-Run-Id", "run-A");
        var resA1 = await client.PostAsJsonAsync("/api/test/db-write", new SyntheticWriteRequest("run-A", "scenario-1", "data-A1"));
        var resA2 = await client.PostAsJsonAsync("/api/test/db-write", new SyntheticWriteRequest("run-A", "scenario-1", "data-A2"));
        Assert.Equal(HttpStatusCode.Created, resA1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, resA2.StatusCode);

        // Write 1 record for run-B
        client.DefaultRequestHeaders.Remove("X-Test-Run-Id");
        client.DefaultRequestHeaders.Add("X-Test-Run-Id", "run-B");
        var resB1 = await client.PostAsJsonAsync("/api/test/db-write", new SyntheticWriteRequest("run-B", "scenario-1", "data-B1"));
        Assert.Equal(HttpStatusCode.Created, resB1.StatusCode);

        // Delete only run-A
        var delRes = await client.DeleteAsync("/api/test/runs/run-A");
        Assert.Equal(HttpStatusCode.OK, delRes.StatusCode);

        // Verify deleting run-A again deletes 0 records (it was cleaned up)
        var delResAgain = await client.DeleteAsync("/api/test/runs/run-A");
        Assert.Equal(HttpStatusCode.OK, delResAgain.StatusCode);

        // Deleting run-B should delete 1 record (verifying run-B was preserved during run-A cleanup!)
        var delResB = await client.DeleteAsync("/api/test/runs/run-B");
        Assert.Equal(HttpStatusCode.OK, delResB.StatusCode);
        var contentB = await delResB.Content.ReadAsStringAsync();
        Assert.Contains("\"deletedRecords\":1", contentB);
    }

    [Fact]
    public async Task TestEndpoints_EnforcesMaxSyntheticWritesPerRun()
    {
        using var factory = new ConfigurableTestFactory(enabled: true, allowWrites: true, maxWrites: 2);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Run-Id", "quota-run");

        var res1 = await client.PostAsJsonAsync("/api/test/db-write", new SyntheticWriteRequest("quota-run", "scenario-1", "data-1"));
        var res2 = await client.PostAsJsonAsync("/api/test/db-write", new SyntheticWriteRequest("quota-run", "scenario-1", "data-2"));
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);
        Assert.Equal(HttpStatusCode.Created, res2.StatusCode);

        // 3rd write exceeds maxWrites (2)
        var res3 = await client.PostAsJsonAsync("/api/test/db-write", new SyntheticWriteRequest("quota-run", "scenario-1", "data-3"));
        Assert.Equal(HttpStatusCode.TooManyRequests, res3.StatusCode);

        // Cleanup
        await client.DeleteAsync("/api/test/runs/quota-run");
    }
}
