using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

public static class ObservabilityMetrics
{
    public const string MeterName = "MonolithApi";
    public const string ActivitySourceName = "MonolithApi";

    public static readonly Meter Meter = new(MeterName, "1.0.0");
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    // Rate Limiting Counters
    public static readonly Counter<long> RateLimitAccepted =
        Meter.CreateCounter<long>(
            "rate_limit_accepted_total",
            description: "Total number of requests accepted by the rate limiter");

    public static readonly Counter<long> RateLimitRejected =
        Meter.CreateCounter<long>(
            "rate_limit_rejected_total",
            description: "Total number of requests rejected by the rate limiter");

    // In-Flight Requests Gauge / UpDownCounter
    public static readonly UpDownCounter<long> InFlightRequests =
        Meter.CreateUpDownCounter<long>(
            "http_server_in_flight_requests",
            description: "Current number of in-flight HTTP requests being processed");

    // Business Operation Counters
    public static readonly Counter<long> OrdersCreated =
        Meter.CreateCounter<long>(
            "orders_created_total",
            description: "Total number of orders successfully placed");

    public static readonly Counter<long> SyntheticWrites =
        Meter.CreateCounter<long>(
            "synthetic_writes_total",
            description: "Total number of synthetic test write records created");
}
