using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Endpoints;
using Xunit;

namespace Api.IntegrationTests;

public class OrderEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OrderEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateOrder_InvalidEmail_ReturnsBadRequest()
    {
        var request = new CreateOrderRequest(
            CustomerEmail: "not-an-email",
            Items: new List<CreateOrderItemRequest> { new(1, 1) });

        var response = await _client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_EmptyItems_ReturnsBadRequest()
    {
        var request = new CreateOrderRequest(
            CustomerEmail: "customer@example.com",
            Items: new List<CreateOrderItemRequest>());

        var response = await _client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_NonExistentProduct_ReturnsBadRequest()
    {
        var request = new CreateOrderRequest(
            CustomerEmail: "customer@example.com",
            Items: new List<CreateOrderItemRequest> { new(99999, 1) });

        var response = await _client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Product Not Found", content);
    }

    [Fact]
    public async Task CreateOrder_InsufficientStock_ReturnsBadRequest()
    {
        // Widget Beta (Id 2) has stock 5
        var request = new CreateOrderRequest(
            CustomerEmail: "customer@example.com",
            Items: new List<CreateOrderItemRequest> { new(2, 50) });

        var response = await _client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Insufficient Stock", content);
    }

    [Fact]
    public async Task CreateOrder_ValidRequest_CreatesOrderAndDeductsStock()
    {
        // Product 1 (Widget Alpha) has initial stock 100, price 19.99
        var initialProdRes = await _client.GetAsync("/api/products/1");
        var initialProd = await initialProdRes.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(initialProd);
        var initialStock = initialProd.StockQuantity;

        var request = new CreateOrderRequest(
            CustomerEmail: "buyer@example.com",
            Items: new List<CreateOrderItemRequest> { new(1, 3) });

        var response = await _client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.Equal("buyer@example.com", order.CustomerEmail);
        Assert.Equal(3 * 19.99m, order.TotalAmount);
        Assert.Single(order.Items);
        Assert.StartsWith("ORD-", order.OrderNumber);

        // Verify stock decremented
        var updatedProdRes = await _client.GetAsync("/api/products/1");
        var updatedProd = await updatedProdRes.Content.ReadFromJsonAsync<ProductDto>();
        Assert.NotNull(updatedProd);
        Assert.Equal(initialStock - 3, updatedProd.StockQuantity);

        // Verify fetch order by ID
        var fetchOrderRes = await _client.GetAsync($"/api/orders/{order.Id}");
        Assert.Equal(HttpStatusCode.OK, fetchOrderRes.StatusCode);
    }
}
