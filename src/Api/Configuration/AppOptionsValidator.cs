namespace Api.Configuration;

public static class AppOptionsValidator
{
    private static readonly HashSet<string> ValidPolicies = new(StringComparer.OrdinalIgnoreCase)
    {
        "FixedWindow",
        "SlidingWindow",
        "TokenBucket",
        "Concurrency"
    };

    public static void Validate(
        ApplicationOptions application,
        DatabaseOptions database,
        RateLimitingOptions rateLimiting,
        RequestLimitsOptions requestLimits,
        TestEndpointsOptions testEndpoints,
        ObservabilityOptions observability,
        string hostEnvironment)
    {
        var errors = new List<string>();

        // Application
        if (string.IsNullOrWhiteSpace(application.Name))
            errors.Add("Application:Name cannot be empty.");

        // Database
        if (database.PoolMinSize < 0)
            errors.Add($"Database:PoolMinSize must be non-negative, got {database.PoolMinSize}.");
        if (database.PoolMaxSize <= 0)
            errors.Add($"Database:PoolMaxSize must be greater than 0, got {database.PoolMaxSize}.");
        if (database.PoolMaxSize < database.PoolMinSize)
            errors.Add($"Database:PoolMaxSize ({database.PoolMaxSize}) cannot be less than PoolMinSize ({database.PoolMinSize}).");
        if (database.CommandTimeoutSeconds <= 0)
            errors.Add($"Database:CommandTimeoutSeconds must be greater than 0, got {database.CommandTimeoutSeconds}.");

        // Rate Limiting
        if (rateLimiting.Enabled)
        {
            if (string.IsNullOrWhiteSpace(rateLimiting.Policy) || !ValidPolicies.Contains(rateLimiting.Policy))
            {
                errors.Add($"RateLimiting:Policy '{rateLimiting.Policy}' is invalid. Supported policies: {string.Join(", ", ValidPolicies)}.");
            }
            else
            {
                var policy = rateLimiting.Policy.ToLowerInvariant();
                if (policy == "fixedwindow")
                {
                    if (rateLimiting.PermitLimit <= 0)
                        errors.Add($"RateLimiting:PermitLimit must be greater than 0 for FixedWindow, got {rateLimiting.PermitLimit}.");
                    if (rateLimiting.WindowSeconds <= 0)
                        errors.Add($"RateLimiting:WindowSeconds must be greater than 0 for FixedWindow, got {rateLimiting.WindowSeconds}.");
                }
                else if (policy == "slidingwindow")
                {
                    if (rateLimiting.PermitLimit <= 0)
                        errors.Add($"RateLimiting:PermitLimit must be greater than 0 for SlidingWindow, got {rateLimiting.PermitLimit}.");
                    if (rateLimiting.WindowSeconds <= 0)
                        errors.Add($"RateLimiting:WindowSeconds must be greater than 0 for SlidingWindow, got {rateLimiting.WindowSeconds}.");
                    if (rateLimiting.SegmentsPerWindow <= 0)
                        errors.Add($"RateLimiting:SegmentsPerWindow must be greater than 0 for SlidingWindow, got {rateLimiting.SegmentsPerWindow}.");
                }
                else if (policy == "tokenbucket")
                {
                    if (rateLimiting.TokenLimit <= 0)
                        errors.Add($"RateLimiting:TokenLimit must be greater than 0 for TokenBucket, got {rateLimiting.TokenLimit}.");
                    if (rateLimiting.TokensPerPeriod <= 0)
                        errors.Add($"RateLimiting:TokensPerPeriod must be greater than 0 for TokenBucket, got {rateLimiting.TokensPerPeriod}.");
                    if (rateLimiting.ReplenishmentPeriodSeconds <= 0)
                        errors.Add($"RateLimiting:ReplenishmentPeriodSeconds must be greater than 0 for TokenBucket, got {rateLimiting.ReplenishmentPeriodSeconds}.");
                }
                else if (policy == "concurrency")
                {
                    if (rateLimiting.ConcurrencyPermitLimit <= 0)
                        errors.Add($"RateLimiting:ConcurrencyPermitLimit must be greater than 0 for Concurrency, got {rateLimiting.ConcurrencyPermitLimit}.");
                }
            }

            if (rateLimiting.QueueLimit < 0)
                errors.Add($"RateLimiting:QueueLimit must be non-negative, got {rateLimiting.QueueLimit}.");

            if (rateLimiting.RejectionStatusCode < 400 || rateLimiting.RejectionStatusCode > 599)
                errors.Add($"RateLimiting:RejectionStatusCode must be a valid HTTP error status code (4xx or 5xx), got {rateLimiting.RejectionStatusCode}.");
        }

        // Request Limits
        if (requestLimits.MaxBodySizeBytes <= 0)
            errors.Add($"RequestLimits:MaxBodySizeBytes must be greater than 0, got {requestLimits.MaxBodySizeBytes}.");
        if (requestLimits.RequestTimeoutSeconds <= 0)
            errors.Add($"RequestLimits:RequestTimeoutSeconds must be greater than 0, got {requestLimits.RequestTimeoutSeconds}.");

        // Test Endpoints
        var isProduction = string.Equals(hostEnvironment, "Production", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(application.Environment, "Production", StringComparison.OrdinalIgnoreCase);

        if (isProduction && testEndpoints.AllowWrites)
        {
            errors.Add("TestEndpoints:AllowWrites cannot be enabled in a Production environment.");
        }

        if (testEndpoints.Enabled)
        {
            if (testEndpoints.MaxSyntheticWritesPerRun <= 0)
                errors.Add($"TestEndpoints:MaxSyntheticWritesPerRun must be greater than 0, got {testEndpoints.MaxSyntheticWritesPerRun}.");
            if (testEndpoints.MaxRunDurationMinutes <= 0)
                errors.Add($"TestEndpoints:MaxRunDurationMinutes must be greater than 0, got {testEndpoints.MaxRunDurationMinutes}.");
        }

        // Observability
        if (observability.TraceSamplingRatio < 0.0 || observability.TraceSamplingRatio > 1.0)
        {
            errors.Add($"Observability:TraceSamplingRatio must be between 0.0 and 1.0, got {observability.TraceSamplingRatio}.");
        }

        if (observability.UseOtlpExporter)
        {
            if (string.IsNullOrWhiteSpace(observability.OtlpEndpoint) ||
                !Uri.TryCreate(observability.OtlpEndpoint, UriKind.Absolute, out _))
            {
                errors.Add($"Observability:OtlpEndpoint must be a valid absolute URI, got '{observability.OtlpEndpoint}'.");
            }
        }

        if (observability.EnablePrometheusExporter)
        {
            if (string.IsNullOrWhiteSpace(observability.PrometheusPath) || !observability.PrometheusPath.StartsWith("/"))
            {
                errors.Add($"Observability:PrometheusPath must start with '/', got '{observability.PrometheusPath}'.");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"Application configuration validation failed with {errors.Count} error(s):\n - " +
                                                string.Join("\n - ", errors));
        }
    }
}
