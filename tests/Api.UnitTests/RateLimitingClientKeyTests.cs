using System.Net;
using System.Security.Claims;
using Api.Configuration;
using Api.RateLimiting;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Api.UnitTests;

public class RateLimitingClientKeyTests
{
    private readonly RateLimitingOptions _options = new()
    {
        TrustedClientHeaderName = "X-Test-Client",
        TestHeaderAllowedOnlyInDevelopment = true
    };

    [Fact]
    public void ExtractClientKey_InDevelopment_WithHeader_ReturnsHeaderValue()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Test-Client"] = "client-alpha";
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.100");

        var key = RateLimitingExtensions.ExtractClientKey(context, _options, isDevelopmentOrTesting: true);
        Assert.Equal("client-alpha", key);
    }

    [Fact]
    public void ExtractClientKey_InProduction_WithHeader_IgnoresHeaderAndUsesIp()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Test-Client"] = "client-alpha";
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.100");

        var key = RateLimitingExtensions.ExtractClientKey(context, _options, isDevelopmentOrTesting: false);
        Assert.Equal("192.168.1.100", key);
    }

    [Fact]
    public void ExtractClientKey_WithAuthenticatedUser_ReturnsUserName()
    {
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "alice@example.com") }, "TestAuth");
        context.User = new ClaimsPrincipal(identity);
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.100");

        var key = RateLimitingExtensions.ExtractClientKey(context, _options, isDevelopmentOrTesting: false);
        Assert.Equal("alice@example.com", key);
    }

    [Fact]
    public void ExtractClientKey_NoHeaderNoAuth_ReturnsRemoteIp()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.5");

        var key = RateLimitingExtensions.ExtractClientKey(context, _options, isDevelopmentOrTesting: true);
        Assert.Equal("10.0.0.5", key);
    }
}
