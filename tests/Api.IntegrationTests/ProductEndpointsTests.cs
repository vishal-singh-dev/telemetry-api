using System.Net;
using System.Text.Json;
using Api.Endpoints;
using Xunit;

namespace Api.IntegrationTests;

public class ProductEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProductEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetProducts_ReturnsPaginatedProducts()
    {
        var response = await _client.GetAsync("/api/products?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<PagedResult<ProductDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.True(result.Items.Count > 0);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 150)]
    public async Task GetProducts_InvalidPagination_ReturnsBadRequest(int page, int pageSize)
    {
        var response = await _client.GetAsync($"/api/products?page={page}&pageSize={pageSize}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetProductById_ExistingProduct_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/products/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var product = JsonSerializer.Deserialize<ProductDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(product);
        Assert.Equal(1, product.Id);
        Assert.Equal("Widget Alpha", product.Name);
    }

    [Fact]
    public async Task GetProductById_NonExistent_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/products/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
