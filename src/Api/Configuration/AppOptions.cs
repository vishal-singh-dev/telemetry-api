namespace Api.Configuration;

public class AppOptions
{
    public ApplicationOptions Application { get; set; } = new();
    public DatabaseOptions Database { get; set; } = new();
    public RateLimitingOptions RateLimiting { get; set; } = new();
    public RequestLimitsOptions RequestLimits { get; set; } = new();
    public TestEndpointsOptions TestEndpoints { get; set; } = new();
    public ObservabilityOptions Observability { get; set; } = new();
    public CorsOptions Cors { get; set; } = new();
}

public class ApplicationOptions
{
    public const string SectionName = "Application";

    public string Name { get; set; } = "MonolithObservabilityLab";
    public string Environment { get; set; } = "Development";
    public string PublicBaseUrl { get; set; } = "http://localhost:8080";
    public bool EnableSwagger { get; set; } = true;
}

public class DatabaseOptions
{
    public const string SectionName = "Database";

    public bool ApplyMigrationsOnStartup { get; set; } = false;
    public int CommandTimeoutSeconds { get; set; } = 30;
    public int MaxRetryCount { get; set; } = 3;
    public int MaxRetryDelaySeconds { get; set; } = 5;
    public bool EnableSensitiveDataLogging { get; set; } = false;
    public int PoolMinSize { get; set; } = 0;
    public int PoolMaxSize { get; set; } = 100;
}

public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;
    public string Policy { get; set; } = "FixedWindow"; // FixedWindow, SlidingWindow, TokenBucket, Concurrency
    public int PermitLimit { get; set; } = 100;
    public int WindowSeconds { get; set; } = 60;
    public int SegmentsPerWindow { get; set; } = 6;
    public int TokenLimit { get; set; } = 100;
    public int TokensPerPeriod { get; set; } = 100;
    public int ReplenishmentPeriodSeconds { get; set; } = 60;
    public int QueueLimit { get; set; } = 0;
    public bool AutoReplenishment { get; set; } = true;
    public int RejectionStatusCode { get; set; } = 429;
    public bool IncludeRetryAfterHeader { get; set; } = true;
    public string PartitionBy { get; set; } = "ClientKey";
    public string TrustedClientHeaderName { get; set; } = "X-Test-Client";
    public bool TestHeaderAllowedOnlyInDevelopment { get; set; } = true;
    public int ConcurrencyPermitLimit { get; set; } = 100;
    public string[] ExcludedPaths { get; set; } = new[]
    {
        "/health/live",
        "/health/ready",
        "/metrics"
    };
}

public class RequestLimitsOptions
{
    public const string SectionName = "RequestLimits";

    public long MaxBodySizeBytes { get; set; } = 1048576; // 1 MB
    public int RequestTimeoutSeconds { get; set; } = 30;
}

public class TestEndpointsOptions
{
    public const string SectionName = "TestEndpoints";

    public bool Enabled { get; set; } = false;
    public bool AllowWrites { get; set; } = false;
    public int MaxSyntheticWritesPerRun { get; set; } = 100000;
    public int MaxRunDurationMinutes { get; set; } = 30;
    public bool RequireTestRunId { get; set; } = true;
}

public class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public string ServiceName { get; set; } = "monolith-api";
    public string ServiceVersion { get; set; } = "1.0.0";
    public string OtlpEndpoint { get; set; } = "http://otel-collector:4317";
    public bool UseOtlpExporter { get; set; } = true;
    public bool EnableAspNetCoreInstrumentation { get; set; } = true;
    public bool EnableHttpClientInstrumentation { get; set; } = true;
    public bool EnableRuntimeInstrumentation { get; set; } = true;
    public bool EnableNpgsqlInstrumentation { get; set; } = true;
    public bool EnablePrometheusExporter { get; set; } = true;
    public string PrometheusPath { get; set; } = "/metrics";
    public double TraceSamplingRatio { get; set; } = 0.1;
    public bool CaptureExceptionDetails { get; set; } = true;
    public bool CaptureRequestBody { get; set; } = false;
    public bool CaptureResponseBody { get; set; } = false;
}

public class CorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}
