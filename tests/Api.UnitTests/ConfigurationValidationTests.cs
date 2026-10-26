using Api.Configuration;
using Xunit;

namespace Api.UnitTests;

public class ConfigurationValidationTests
{
    private static (ApplicationOptions App, DatabaseOptions Db, RateLimitingOptions Rate, RequestLimitsOptions Req, TestEndpointsOptions Test, ObservabilityOptions Obs) CreateValidOptions()
    {
        return (
            new ApplicationOptions { Name = "MonolithObservabilityLab", Environment = "Development" },
            new DatabaseOptions { PoolMinSize = 0, PoolMaxSize = 100, CommandTimeoutSeconds = 30 },
            new RateLimitingOptions { Enabled = true, Policy = "FixedWindow", PermitLimit = 100, WindowSeconds = 60, QueueLimit = 0, RejectionStatusCode = 429 },
            new RequestLimitsOptions { MaxBodySizeBytes = 1048576, RequestTimeoutSeconds = 30 },
            new TestEndpointsOptions { Enabled = true, AllowWrites = false, MaxSyntheticWritesPerRun = 1000, MaxRunDurationMinutes = 30 },
            new ObservabilityOptions { TraceSamplingRatio = 0.1, OtlpEndpoint = "http://localhost:4317", UseOtlpExporter = false, EnablePrometheusExporter = true, PrometheusPath = "/metrics" }
        );
    }

    [Fact]
    public void Validate_ValidConfiguration_DoesNotThrow()
    {
        var (app, db, rate, req, test, obs) = CreateValidOptions();
        var ex = Record.Exception(() => AppOptionsValidator.Validate(app, db, rate, req, test, obs, "Development"));
        Assert.Null(ex);
    }

    [Fact]
    public void Validate_EmptyApplicationName_ThrowsException()
    {
        var (app, db, rate, req, test, obs) = CreateValidOptions();
        app.Name = "";
        Assert.Throws<InvalidOperationException>(() => AppOptionsValidator.Validate(app, db, rate, req, test, obs, "Development"));
    }

    [Fact]
    public void Validate_PoolMaxLessThanMin_ThrowsException()
    {
        var (app, db, rate, req, test, obs) = CreateValidOptions();
        db.PoolMinSize = 50;
        db.PoolMaxSize = 10;
        var ex = Assert.Throws<InvalidOperationException>(() => AppOptionsValidator.Validate(app, db, rate, req, test, obs, "Development"));
        Assert.Contains("PoolMaxSize (10) cannot be less than PoolMinSize (50)", ex.Message);
    }

    [Fact]
    public void Validate_NegativePoolMinSize_ThrowsException()
    {
        var (app, db, rate, req, test, obs) = CreateValidOptions();
        db.PoolMinSize = -1;
        Assert.Throws<InvalidOperationException>(() => AppOptionsValidator.Validate(app, db, rate, req, test, obs, "Development"));
    }

    [Fact]
    public void Validate_UnknownRateLimitingPolicy_ThrowsException()
    {
        var (app, db, rate, req, test, obs) = CreateValidOptions();
        rate.Policy = "NonExistentPolicy";
        var ex = Assert.Throws<InvalidOperationException>(() => AppOptionsValidator.Validate(app, db, rate, req, test, obs, "Development"));
        Assert.Contains("RateLimiting:Policy 'NonExistentPolicy' is invalid", ex.Message);
    }

    [Theory]
    [InlineData("FixedWindow", 0, 60)]
    [InlineData("FixedWindow", 100, 0)]
    public void Validate_InvalidFixedWindowOptions_ThrowsException(string policy, int permitLimit, int windowSeconds)
    {
        var (app, db, rate, req, test, obs) = CreateValidOptions();
        rate.Policy = policy;
        rate.PermitLimit = permitLimit;
        rate.WindowSeconds = windowSeconds;
        Assert.Throws<InvalidOperationException>(() => AppOptionsValidator.Validate(app, db, rate, req, test, obs, "Development"));
    }

    [Fact]
    public void Validate_InvalidSlidingWindowSegments_ThrowsException()
    {
        var (app, db, rate, req, test, obs) = CreateValidOptions();
        rate.Policy = "SlidingWindow";
        rate.SegmentsPerWindow = 0;
        Assert.Throws<InvalidOperationException>(() => AppOptionsValidator.Validate(app, db, rate, req, test, obs, "Development"));
    }

    [Fact]
    public void Validate_InvalidTokenBucketReplenishment_ThrowsException()
    {
        var (app, db, rate, req, test, obs) = CreateValidOptions();
        rate.Policy = "TokenBucket";
        rate.ReplenishmentPeriodSeconds = -5;
        Assert.Throws<InvalidOperationException>(() => AppOptionsValidator.Validate(app, db, rate, req, test, obs, "Development"));
    }

    [Fact]
    public void Validate_InvalidConcurrencyPermitLimit_ThrowsException()
    {
        var (app, db, rate, req, test, obs) = CreateValidOptions();
        rate.Policy = "Concurrency";
        rate.ConcurrencyPermitLimit = 0;
        Assert.Throws<InvalidOperationException>(() => AppOptionsValidator.Validate(app, db, rate, req, test, obs, "Development"));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Validate_InvalidTraceSamplingRatio_ThrowsException(double ratio)
    {
        var (app, db, rate, req, test, obs) = CreateValidOptions();
        obs.TraceSamplingRatio = ratio;
        var ex = Assert.Throws<InvalidOperationException>(() => AppOptionsValidator.Validate(app, db, rate, req, test, obs, "Development"));
        Assert.Contains("TraceSamplingRatio must be between 0.0 and 1.0", ex.Message);
    }

    [Fact]
    public void Validate_TestWritesEnabledInProduction_ThrowsException()
    {
        var (app, db, rate, req, test, obs) = CreateValidOptions();
        test.AllowWrites = true;
        var ex = Assert.Throws<InvalidOperationException>(() => AppOptionsValidator.Validate(app, db, rate, req, test, obs, "Production"));
        Assert.Contains("TestEndpoints:AllowWrites cannot be enabled in a Production environment", ex.Message);
    }

    [Fact]
    public void Validate_InvalidOtlpEndpoint_ThrowsException()
    {
        var (app, db, rate, req, test, obs) = CreateValidOptions();
        obs.UseOtlpExporter = true;
        obs.OtlpEndpoint = "not-a-valid-uri";
        var ex = Assert.Throws<InvalidOperationException>(() => AppOptionsValidator.Validate(app, db, rate, req, test, obs, "Development"));
        Assert.Contains("OtlpEndpoint must be a valid absolute URI", ex.Message);
    }
}
