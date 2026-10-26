using System.Threading.RateLimiting;
using Api.Configuration;
using Api.Observability;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Api.RateLimiting;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddConfiguredRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(_ => { });

        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitingOptions>, IWebHostEnvironment>((limiterOptions, rateLimitOptionsAccessor, env) =>
            {
                var options = rateLimitOptionsAccessor.Value;

                if (!options.Enabled)
                {
                    limiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                        RateLimitPartition.GetNoLimiter("disabled"));
                    return;
                }

                limiterOptions.RejectionStatusCode = options.RejectionStatusCode;
                var isDevOrTest = env.IsDevelopment() || env.EnvironmentName == "Testing";

                limiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                {
                    var path = httpContext.Request.Path.Value ?? string.Empty;

                    // Check excluded paths
                    foreach (var excluded in options.ExcludedPaths)
                    {
                        if (path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase))
                        {
                            return RateLimitPartition.GetNoLimiter("excluded");
                        }
                    }

                    var clientKey = ExtractClientKey(httpContext, options, isDevOrTest);
                    var policy = options.Policy.ToLowerInvariant();

                    return policy switch
                    {
                        "fixedwindow" => RateLimitPartition.GetFixedWindowLimiter(clientKey, _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = options.PermitLimit,
                            Window = TimeSpan.FromSeconds(options.WindowSeconds),
                            QueueLimit = options.QueueLimit,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            AutoReplenishment = options.AutoReplenishment
                        }),
                        "slidingwindow" => RateLimitPartition.GetSlidingWindowLimiter(clientKey, _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = options.PermitLimit,
                            Window = TimeSpan.FromSeconds(options.WindowSeconds),
                            SegmentsPerWindow = options.SegmentsPerWindow,
                            QueueLimit = options.QueueLimit,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            AutoReplenishment = options.AutoReplenishment
                        }),
                        "tokenbucket" => RateLimitPartition.GetTokenBucketLimiter(clientKey, _ => new TokenBucketRateLimiterOptions
                        {
                            TokenLimit = options.TokenLimit,
                            TokensPerPeriod = options.TokensPerPeriod,
                            ReplenishmentPeriod = TimeSpan.FromSeconds(options.ReplenishmentPeriodSeconds),
                            QueueLimit = options.QueueLimit,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            AutoReplenishment = options.AutoReplenishment
                        }),
                        "concurrency" => RateLimitPartition.GetConcurrencyLimiter(clientKey, _ => new ConcurrencyLimiterOptions
                        {
                            PermitLimit = options.ConcurrencyPermitLimit,
                            QueueLimit = options.QueueLimit,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                        }),
                        _ => RateLimitPartition.GetNoLimiter("unsupported")
                    };
                });

                limiterOptions.OnRejected = async (context, cancellationToken) =>
                {
                    var status = options.RejectionStatusCode;
                    context.HttpContext.Response.StatusCode = status;
                    ObservabilityMetrics.RateLimitRejected.Add(1, new KeyValuePair<string, object?>("policy", options.Policy));

                    if (options.IncludeRetryAfterHeader)
                    {
                        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                        {
                            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Max(1, retryAfter.TotalSeconds)).ToString();
                        }
                        else
                        {
                            context.HttpContext.Response.Headers.RetryAfter = options.WindowSeconds.ToString();
                        }
                    }

                    context.HttpContext.Response.ContentType = "application/json";
                    await context.HttpContext.Response.WriteAsync(
                        System.Text.Json.JsonSerializer.Serialize(new
                        {
                            error = "Too Many Requests",
                            message = "Rate limit exceeded. Please retry later.",
                            statusCode = status
                        }),
                        cancellationToken);
                };
            });

        return services;
    }

    public static string ExtractClientKey(HttpContext context, RateLimitingOptions options, bool isDevelopmentOrTesting)
    {
        if (isDevelopmentOrTesting && options.TestHeaderAllowedOnlyInDevelopment)
        {
            if (context.Request.Headers.TryGetValue(options.TrustedClientHeaderName, out var headerVal) &&
                !string.IsNullOrWhiteSpace(headerVal))
            {
                return headerVal.ToString().Trim();
            }
        }

        if (context.User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(context.User.Identity.Name))
        {
            return context.User.Identity.Name;
        }

        var ip = context.Connection.RemoteIpAddress?.ToString();
        return !string.IsNullOrWhiteSpace(ip) ? ip : "unknown-client";
    }
}
