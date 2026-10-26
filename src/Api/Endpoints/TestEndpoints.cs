using Api.Configuration;
using Api.Data;
using Api.Data.Entities;
using Api.Observability;
using Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Endpoints;

public static class TestEndpoints
{
    public static IEndpointRouteBuilder MapTestEndpoints(this IEndpointRouteBuilder app, IWebHostEnvironment environment)
    {
        var group = app.MapGroup("/api/test").WithTags("TestEndpoints");

        // GET /api/test/db-read
        group.MapGet("/db-read", async (
            [FromServices] AppDbContext db,
            [FromServices] IOptions<TestEndpointsOptions> testOpts,
            CancellationToken ct) =>
        {
            if (!testOpts.Value.Enabled)
            {
                return Results.Json(new
                {
                    error = "Forbidden",
                    message = "Synthetic test endpoints are disabled by configuration (TestEndpoints:Enabled=false)."
                }, statusCode: StatusCodes.Status403Forbidden);
            }

            var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(ct);
            return Results.Ok(new
            {
                status = "Success",
                readType = "synthetic-db-read",
                timestampUtc = DateTime.UtcNow,
                sampleRecord = product is not null
                    ? new { product.Id, product.Name, product.Price }
                    : null
            });
        });

        // POST /api/test/db-write
        group.MapPost("/db-write", async (
            HttpContext context,
            [FromBody] SyntheticWriteRequest? request,
            [FromServices] AppDbContext db,
            [FromServices] IOptions<TestEndpointsOptions> testOpts,
            [FromServices] IOptions<RateLimitingOptions> rateOpts,
            CancellationToken ct) =>
        {
            var options = testOpts.Value;

            if (!options.Enabled)
            {
                return Results.Json(new
                {
                    error = "Forbidden",
                    message = "Synthetic test endpoints are disabled by configuration (TestEndpoints:Enabled=false)."
                }, statusCode: StatusCodes.Status403Forbidden);
            }

            if (!options.AllowWrites)
            {
                return Results.Json(new
                {
                    error = "Forbidden",
                    message = "Synthetic writes are disabled by configuration (TestEndpoints:AllowWrites=false)."
                }, statusCode: StatusCodes.Status403Forbidden);
            }

            if (environment.IsProduction())
            {
                return Results.Json(new
                {
                    error = "Forbidden",
                    message = "Synthetic writes are strictly prohibited in a Production environment."
                }, statusCode: StatusCodes.Status403Forbidden);
            }

            // Extract TestRunId
            string? testRunId = null;
            if (context.Request.Headers.TryGetValue("X-Test-Run-Id", out var runHeaderVal) &&
                !string.IsNullOrWhiteSpace(runHeaderVal))
            {
                testRunId = runHeaderVal.ToString().Trim();
            }
            else if (!string.IsNullOrWhiteSpace(request?.TestRunId))
            {
                testRunId = request.TestRunId.Trim();
            }

            if (options.RequireTestRunId && string.IsNullOrWhiteSpace(testRunId))
            {
                return Results.BadRequest(new
                {
                    error = "Validation Error",
                    message = "A valid TestRunId is required via 'X-Test-Run-Id' header or payload when RequireTestRunId is enabled."
                });
            }

            testRunId ??= $"default-run-{DateTime.UtcNow:yyyyMMdd}";

            // Check max writes for this run
            var currentRunWrites = await db.LoadTestRecords
                .Where(r => r.TestRunId == testRunId)
                .CountAsync(ct);

            if (currentRunWrites >= options.MaxSyntheticWritesPerRun)
            {
                return Results.Json(new
                {
                    error = "Quota Exceeded",
                    message = $"Maximum synthetic writes per run ({options.MaxSyntheticWritesPerRun}) reached for TestRunId '{testRunId}'."
                }, statusCode: StatusCodes.Status429TooManyRequests);
            }

            // Check max run duration
            var firstRecord = await db.LoadTestRecords
                .Where(r => r.TestRunId == testRunId)
                .OrderBy(r => r.CreatedAtUtc)
                .Select(r => r.CreatedAtUtc)
                .FirstOrDefaultAsync(ct);

            if (firstRecord != default)
            {
                var runDuration = DateTime.UtcNow - firstRecord;
                if (runDuration.TotalMinutes > options.MaxRunDurationMinutes)
                {
                    return Results.Json(new
                    {
                        error = "Run Expired",
                        message = $"Test run '{testRunId}' duration has exceeded the maximum allowed limit of {options.MaxRunDurationMinutes} minutes."
                    }, statusCode: StatusCodes.Status400BadRequest);
                }
            }

            var clientKey = RateLimitingExtensions.ExtractClientKey(
                context,
                rateOpts.Value,
                environment.IsDevelopment() || environment.EnvironmentName == "Testing");

            var record = new LoadTestRecord
            {
                TestRunId = testRunId,
                ClientKey = clientKey,
                Scenario = request?.Scenario ?? "synthetic-write",
                Payload = request?.Payload ?? "load-test-payload",
                CreatedAtUtc = DateTime.UtcNow
            };

            db.LoadTestRecords.Add(record);
            await db.SaveChangesAsync(ct);

            ObservabilityMetrics.SyntheticWrites.Add(1);

            return Results.Created(
                $"/api/test/runs/{testRunId}",
                new SyntheticWriteResponse(record.Id, record.TestRunId, record.Scenario, record.CreatedAtUtc));
        });

        // DELETE /api/test/runs/{testRunId}
        group.MapDelete("/runs/{testRunId}", async (
            string testRunId,
            [FromServices] AppDbContext db,
            [FromServices] IOptions<TestEndpointsOptions> testOpts,
            CancellationToken ct) =>
        {
            if (!testOpts.Value.Enabled)
            {
                return Results.Json(new
                {
                    error = "Forbidden",
                    message = "Synthetic test endpoints are disabled by configuration."
                }, statusCode: StatusCodes.Status403Forbidden);
            }

            if (string.IsNullOrWhiteSpace(testRunId))
            {
                return Results.BadRequest(new { error = "Validation Error", message = "testRunId path parameter is required." });
            }

            // Strictly scoped cleanup to testRunId only
            var recordsToDelete = await db.LoadTestRecords
                .Where(r => r.TestRunId == testRunId)
                .ToListAsync(ct);

            var deletedCount = recordsToDelete.Count;
            if (deletedCount > 0)
            {
                db.LoadTestRecords.RemoveRange(recordsToDelete);
                await db.SaveChangesAsync(ct);
            }

            return Results.Ok(new
            {
                status = "Cleaned",
                testRunId = testRunId,
                deletedRecords = deletedCount,
                timestampUtc = DateTime.UtcNow
            });
        });

        return app;
    }
}
