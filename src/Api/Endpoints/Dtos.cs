using System.ComponentModel.DataAnnotations;

namespace Api.Endpoints;

public record ProductDto(
    long Id,
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record CreateOrderItemRequest(
    [Required] long ProductId,
    [Range(1, 1000)] int Quantity);

public record CreateOrderRequest(
    [Required, EmailAddress] string CustomerEmail,
    [Required, MinLength(1)] List<CreateOrderItemRequest> Items);

public record OrderItemDto(
    long Id,
    long ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public record OrderDto(
    long Id,
    string OrderNumber,
    string CustomerEmail,
    decimal TotalAmount,
    string Status,
    DateTime CreatedAtUtc,
    IReadOnlyList<OrderItemDto> Items);

public record SyntheticWriteRequest(
    string? TestRunId,
    string? Scenario,
    string? Payload);

public record SyntheticWriteResponse(
    long Id,
    string TestRunId,
    string Scenario,
    DateTime CreatedAtUtc);
