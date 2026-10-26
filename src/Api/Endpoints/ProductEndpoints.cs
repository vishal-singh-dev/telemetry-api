using Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products").WithTags("Products");

        // GET /api/products
        group.MapGet("/", async (
            [FromServices] AppDbContext db,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken ct = default) =>
        {
            if (page < 1)
            {
                return Results.BadRequest(new { error = "Invalid Page", message = "Page must be greater than or equal to 1." });
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return Results.BadRequest(new { error = "Invalid PageSize", message = "PageSize must be between 1 and 100." });
            }

            var query = db.Products.AsNoTracking();
            var totalCount = await query.CountAsync(ct);
            var items = await query
                .OrderBy(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductDto(
                    p.Id,
                    p.Name,
                    p.Description,
                    p.Price,
                    p.StockQuantity,
                    p.CreatedAtUtc,
                    p.UpdatedAtUtc))
                .ToListAsync(ct);

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return Results.Ok(new PagedResult<ProductDto>(items, totalCount, page, pageSize, totalPages));
        });

        // GET /api/products/{id}
        group.MapGet("/{id:long}", async (
            long id,
            [FromServices] AppDbContext db,
            CancellationToken ct = default) =>
        {
            var product = await db.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            if (product is null)
            {
                return Results.NotFound(new { error = "Not Found", message = $"Product with ID {id} was not found." });
            }

            return Results.Ok(new ProductDto(
                product.Id,
                product.Name,
                product.Description,
                product.Price,
                product.StockQuantity,
                product.CreatedAtUtc,
                product.UpdatedAtUtc));
        });

        return app;
    }
}
