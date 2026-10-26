using Api.Configuration;
using Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Health;

public static class HealthEndpoints
{
    private static readonly DateTime StartTimeUtc = DateTime.UtcNow;

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health/live", () =>
        {
            return Results.Ok(new
            {
                status = "Live",
                timestamp = DateTime.UtcNow
            });
        }).WithTags("Health").AllowAnonymous();

        app.MapGet("/health/ready", async ([FromServices] AppDbContext db, CancellationToken ct) =>
        {
            try
            {
                var canConnect = await db.Database.CanConnectAsync(ct);
                if (canConnect)
                {
                    return Results.Ok(new
                    {
                        status = "Ready",
                        database = "Healthy",
                        timestamp = DateTime.UtcNow
                    });
                }

                return Results.Json(new
                {
                    status = "Unhealthy",
                    database = "Unreachable",
                    timestamp = DateTime.UtcNow
                }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (Exception ex)
            {
                return Results.Json(new
                {
                    status = "Unhealthy",
                    database = "Unreachable",
                    error = ex.Message,
                    timestamp = DateTime.UtcNow
                }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).WithTags("Health").AllowAnonymous();

        app.MapGet("/health", async ([FromServices] AppDbContext db, [FromServices] IOptions<ObservabilityOptions> obsOpts, CancellationToken ct) =>
        {
            var dbHealthy = false;
            string? dbError = null;

            try
            {
                dbHealthy = await db.Database.CanConnectAsync(ct);
            }
            catch (Exception ex)
            {
                dbError = ex.Message;
            }

            var overallStatus = dbHealthy ? "Healthy" : "Degraded";
            var uptime = DateTime.UtcNow - StartTimeUtc;

            var result = new
            {
                status = overallStatus,
                service = obsOpts.Value.ServiceName,
                version = obsOpts.Value.ServiceVersion,
                uptimeSeconds = (long)uptime.TotalSeconds,
                timestamp = DateTime.UtcNow,
                components = new
                {
                    process = "Live",
                    database = dbHealthy ? "Healthy" : "Unreachable"
                },
                error = dbError
            };

            return dbHealthy
                ? Results.Ok(result)
                : Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);
        }).WithTags("Health").AllowAnonymous();

        return app;
    }
}
