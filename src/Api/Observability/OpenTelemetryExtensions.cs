using Api.Configuration;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Api.Observability;

public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddConfiguredOpenTelemetry(
        this IServiceCollection services,
        ObservabilityOptions options)
    {
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: options.ServiceName,
                serviceVersion: options.ServiceVersion);

        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing.SetResourceBuilder(resourceBuilder);

                var ratio = Math.Clamp(options.TraceSamplingRatio, 0.0, 1.0);
                tracing.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(ratio)));

                if (options.EnableAspNetCoreInstrumentation)
                {
                    tracing.AddAspNetCoreInstrumentation(opt =>
                    {
                        opt.RecordException = options.CaptureExceptionDetails;
                        // Exclude health checks from noisy tracing if desired
                        opt.Filter = httpContext =>
                        {
                            var path = httpContext.Request.Path.Value ?? string.Empty;
                            return !path.StartsWith("/health/live", StringComparison.OrdinalIgnoreCase);
                        };
                    });
                }

                if (options.EnableHttpClientInstrumentation)
                {
                    tracing.AddHttpClientInstrumentation(opt =>
                    {
                        opt.RecordException = options.CaptureExceptionDetails;
                    });
                }

                if (options.EnableNpgsqlInstrumentation)
                {
                    tracing.AddNpgsql();
                }

                tracing.AddSource(ObservabilityMetrics.ActivitySourceName);

                if (options.UseOtlpExporter && !string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                {
                    tracing.AddOtlpExporter(otlp =>
                    {
                        otlp.Endpoint = new Uri(options.OtlpEndpoint);
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.SetResourceBuilder(resourceBuilder);

                if (options.EnableAspNetCoreInstrumentation)
                {
                    metrics.AddAspNetCoreInstrumentation();
                }

                if (options.EnableHttpClientInstrumentation)
                {
                    metrics.AddHttpClientInstrumentation();
                }

                if (options.EnableRuntimeInstrumentation)
                {
                    metrics.AddRuntimeInstrumentation();
                }

                metrics.AddMeter(ObservabilityMetrics.MeterName);

                if (options.EnablePrometheusExporter)
                {
                    metrics.AddPrometheusExporter();
                }

                if (options.UseOtlpExporter && !string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                {
                    metrics.AddOtlpExporter(otlp =>
                    {
                        otlp.Endpoint = new Uri(options.OtlpEndpoint);
                    });
                }
            });

        return services;
    }
}
