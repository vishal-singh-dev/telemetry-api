using Api.Configuration;
using Microsoft.Extensions.Options;

namespace Api.Observability;

public class ObservabilityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptions<RateLimitingOptions> _rateLimitOptions;
    private readonly IOptions<RequestLimitsOptions> _requestLimitOptions;

    public ObservabilityMiddleware(
        RequestDelegate next,
        IOptions<RateLimitingOptions> rateLimitOptions,
        IOptions<RequestLimitsOptions> requestLimitOptions)
    {
        _next = next;
        _rateLimitOptions = rateLimitOptions;
        _requestLimitOptions = requestLimitOptions;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ObservabilityMetrics.InFlightRequests.Add(1);
        try
        {
            var maxBodySize = _requestLimitOptions.Value.MaxBodySizeBytes;
            if (maxBodySize > 0 && context.Request.ContentLength > maxBodySize)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        error = "Payload Too Large",
                        message = $"Request payload exceeds maximum allowed size of {maxBodySize} bytes.",
                        statusCode = 413
                    }));
                return;
            }

            await _next(context);

            // If rate limiting is enabled and request was not rejected, track as accepted
            var rateOpts = _rateLimitOptions.Value;
            if (rateOpts.Enabled)
            {
                var path = context.Request.Path.Value ?? string.Empty;
                var isExcluded = rateOpts.ExcludedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
                if (!isExcluded && context.Response.StatusCode != rateOpts.RejectionStatusCode)
                {
                    ObservabilityMetrics.RateLimitAccepted.Add(1, new KeyValuePair<string, object?>("policy", rateOpts.Policy));
                }
            }
        }
        finally
        {
            ObservabilityMetrics.InFlightRequests.Add(-1);
        }
    }
}
