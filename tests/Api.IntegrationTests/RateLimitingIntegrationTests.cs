using System.Net;
using System.Net.Http.Json;
using Api.Configuration;
using Api.Endpoints;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.IntegrationTests;

public class RateLimitingIntegrationTests
{
    private class RateLimitedFactory : CustomWebApplicationFactory
    {
        private readonly bool _enabled;
        private readonly int _permitLimit;

        public RateLimitedFactory(bool enabled = true, int permitLimit = 3)
        {
            _enabled = enabled;
            _permitLimit = permitLimit;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.Configure<RateLimitingOptions>(opts =>
                {
                    opts.Enabled = _enabled;
                    opts.Policy = "FixedWindow";
                    opts.PermitLimit = _permitLimit;
                    opts.WindowSeconds = 60;
                    opts.QueueLimit = 0;
                    opts.IncludeRetryAfterHeader = true;
                    opts.PartitionBy = "ClientKey";
                    opts.TrustedClientHeaderName = "X-Test-Client";
                    opts.TestHeaderAllowedOnlyInDevelopment = true;
                    opts.ExcludedPaths = new[] { "/health/live", "/health/ready", "/metrics" };
                });
            });
        }
    }

    [Fact]
    public async Task RateLimiter_EnforcesLimit_AndReturns429WithRetryAfter()
    {
        using var factory = new RateLimitedFactory(enabled: true, permitLimit: 3);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Client", "tester-quota-1");

        // First 3 requests should succeed
        for (int i = 0; i < 3; i++)
        {
            var res = await client.GetAsync("/api/products/1");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        // 4th request must be rejected with 429
        var rejectedRes = await client.GetAsync("/api/products/1");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedRes.StatusCode);
        Assert.True(rejectedRes.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task RateLimiter_PartitionIsolation_SeparatesClientBuckets()
    {
        using var factory = new RateLimitedFactory(enabled: true, permitLimit: 2);
        var client = factory.CreateClient();

        // Exhaust client-A quota
        var reqA1 = new HttpRequestMessage(HttpMethod.Get, "/api/products/1");
        reqA1.Headers.Add("X-Test-Client", "client-A");
        var resA1 = await client.SendAsync(reqA1);
        Assert.Equal(HttpStatusCode.OK, resA1.StatusCode);

        var reqA2 = new HttpRequestMessage(HttpMethod.Get, "/api/products/1");
        reqA2.Headers.Add("X-Test-Client", "client-A");
        var resA2 = await client.SendAsync(reqA2);
        Assert.Equal(HttpStatusCode.OK, resA2.StatusCode);

        // 3rd request for client-A -> 429
        var reqA3 = new HttpRequestMessage(HttpMethod.Get, "/api/products/1");
        reqA3.Headers.Add("X-Test-Client", "client-A");
        var resA3 = await client.SendAsync(reqA3);
        Assert.Equal(HttpStatusCode.TooManyRequests, resA3.StatusCode);

        // Client-B should still have full quota available!
        var reqB1 = new HttpRequestMessage(HttpMethod.Get, "/api/products/1");
        reqB1.Headers.Add("X-Test-Client", "client-B");
        var resB1 = await client.SendAsync(reqB1);
        Assert.Equal(HttpStatusCode.OK, resB1.StatusCode);
    }

    [Fact]
    public async Task RateLimiter_ExcludedPaths_AreNotThrottled()
    {
        using var factory = new RateLimitedFactory(enabled: true, permitLimit: 2);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Client", "excluded-client");

        // Exhaust client quota on API
        var r1 = await client.GetAsync("/api/products/1");
        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        var r2 = await client.GetAsync("/api/products/1");
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);

        var rejected = await client.GetAsync("/api/products/1");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);

        // /health/live is excluded from rate limiting and must continue to succeed
        var liveRes = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, liveRes.StatusCode);

        // /health/ready is also excluded
        var readyRes = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, readyRes.StatusCode);
    }

    [Fact]
    public async Task RateLimiter_DisabledMode_AllowsUnlimitedRequests()
    {
        using var factory = new RateLimitedFactory(enabled: false, permitLimit: 2);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Client", "disabled-test-client");

        // Send 10 requests when permit limit was 2 -> all should succeed because Enabled=false
        for (int i = 0; i < 10; i++)
        {
            var res = await client.GetAsync("/api/products/1");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }
    }

    [Fact]
    public async Task RateLimiter_RejectedRequests_DoNotExecuteProtectedDatabaseWrites()
    {
        using var factory = new RateLimitedFactory(enabled: true, permitLimit: 1);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Client", "write-protection-client");

        // Initial product check
        var prodRes = await client.GetAsync("/api/products/1");
        var prod = await prodRes.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(prod);
        var initialStock = prod.StockQuantity;

        // Quota is 1, so the GET /api/products/1 consumed the 1 permit for this window!
        // Now attempt a protected database write POST /api/orders
        var orderReq = new CreateOrderRequest(
            CustomerEmail: "prevented@example.com",
            Items: new List<CreateOrderItemRequest> { new(1, 5) });

        var rejectedOrderRes = await client.PostAsJsonAsync("/api/orders", orderReq);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedOrderRes.StatusCode);

        // Verify database write was NOT executed and stock was NOT deducted
        var checkClient = factory.CreateClient();
        checkClient.DefaultRequestHeaders.Add("X-Test-Client", "another-client");
        var checkProdRes = await checkClient.GetAsync("/api/products/1");
        var checkProd = await checkProdRes.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(checkProd);
        Assert.Equal(initialStock, checkProd.StockQuantity);
    }
}
