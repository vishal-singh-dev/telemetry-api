using Api.Data;
using Api.Data.Entities;
using Api.Observability;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders");

        group.MapPost("/", async (
            [FromBody] CreateOrderRequest request,
            [FromServices] AppDbContext db,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.CustomerEmail) || !request.CustomerEmail.Contains('@'))
            {
                return Results.BadRequest(new { error = "Validation Error", message = "A valid CustomerEmail is required." });
            }

            if (request.Items is null || request.Items.Count == 0)
            {
                return Results.BadRequest(new { error = "Validation Error", message = "Order must contain at least one item." });
            }

            foreach (var item in request.Items)
            {
                if (item.ProductId <= 0 || item.Quantity <= 0)
                {
                    return Results.BadRequest(new { error = "Validation Error", message = "Each item must have a valid ProductId and Quantity > 0." });
                }
            }

            // Transactional write
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
                var products = await db.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, ct);

                foreach (var itemReq in request.Items)
                {
                    if (!products.TryGetValue(itemReq.ProductId, out var product))
                    {
                        await tx.RollbackAsync(ct);
                        return Results.BadRequest(new
                        {
                            error = "Product Not Found",
                            message = $"Product with ID {itemReq.ProductId} does not exist."
                        });
                    }

                    if (product.StockQuantity < itemReq.Quantity)
                    {
                        await tx.RollbackAsync(ct);
                        return Results.BadRequest(new
                        {
                            error = "Insufficient Stock",
                            message = $"Insufficient stock for product '{product.Name}'. Available: {product.StockQuantity}, Requested: {itemReq.Quantity}."
                        });
                    }

                    // Decrement stock
                    product.StockQuantity -= itemReq.Quantity;
                    product.UpdatedAtUtc = DateTime.UtcNow;
                }

                var now = DateTime.UtcNow;
                var orderNumber = $"ORD-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
                decimal totalAmount = 0;

                var order = new Order
                {
                    OrderNumber = orderNumber,
                    CustomerEmail = request.CustomerEmail.Trim(),
                    Status = "Completed",
                    CreatedAtUtc = now
                };

                foreach (var itemReq in request.Items)
                {
                    var product = products[itemReq.ProductId];
                    var lineTotal = itemReq.Quantity * product.Price;
                    totalAmount += lineTotal;

                    order.Items.Add(new OrderItem
                    {
                        ProductId = product.Id,
                        Product = product,
                        Quantity = itemReq.Quantity,
                        UnitPrice = product.Price
                    });
                }

                order.TotalAmount = totalAmount;

                db.Orders.Add(order);
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                ObservabilityMetrics.OrdersCreated.Add(1);

                var orderDto = new OrderDto(
                    order.Id,
                    order.OrderNumber,
                    order.CustomerEmail,
                    order.TotalAmount,
                    order.Status,
                    order.CreatedAtUtc,
                    order.Items.Select(i => new OrderItemDto(
                        i.Id,
                        i.ProductId,
                        i.Product?.Name ?? products[i.ProductId].Name,
                        i.Quantity,
                        i.UnitPrice,
                        i.Quantity * i.UnitPrice)).ToList());

                return Results.Created($"/api/orders/{order.Id}", orderDto);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                return Results.Problem(detail: ex.Message, title: "Order Processing Failed", statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        // GET /api/orders/{id}
        group.MapGet("/{id:long}", async (
            long id,
            [FromServices] AppDbContext db,
            CancellationToken ct) =>
        {
            var order = await db.Orders
                .Include(o => o.Items)
                .ThenInclude(i => i.Product)
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == id, ct);

            if (order is null)
            {
                return Results.NotFound(new { error = "Not Found", message = $"Order with ID {id} was not found." });
            }

            var orderDto = new OrderDto(
                order.Id,
                order.OrderNumber,
                order.CustomerEmail,
                order.TotalAmount,
                order.Status,
                order.CreatedAtUtc,
                order.Items.Select(i => new OrderItemDto(
                    i.Id,
                    i.ProductId,
                    i.Product?.Name ?? "Unknown",
                    i.Quantity,
                    i.UnitPrice,
                    i.Quantity * i.UnitPrice)).ToList());

            return Results.Ok(orderDto);
        });

        return app;
    }
}
