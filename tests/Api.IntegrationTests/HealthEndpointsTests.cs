using System.Net;
using System.Text.Json;
using Xunit;

namespace Api.IntegrationTests;

public class HealthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealthLive_ReturnsOkWithLiveStatus()
    {
        var response = await _client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("Live", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetHealthReady_ReturnsOkWithReadyAndDatabaseHealthy()
    {
        var response = await _client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("Ready", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("Healthy", doc.RootElement.GetProperty("database").GetString());
    }

    [Fact]
    public async Task GetHealthAggregate_ReturnsOkWithComponentsAndUptime()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.True(doc.RootElement.TryGetProperty("uptimeSeconds", out _));
        Assert.Equal("Live", doc.RootElement.GetProperty("components").GetProperty("process").GetString());
    }
}
