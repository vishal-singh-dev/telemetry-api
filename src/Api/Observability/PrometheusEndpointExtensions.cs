using System.Net;
using Api.Configuration;
using Microsoft.Extensions.Options;

namespace Api.Observability;

public static class PrometheusEndpointExtensions
{
    public static IEndpointRouteBuilder MapRestrictedPrometheusScrapingEndpoint(
        this IEndpointRouteBuilder endpoints,
        ObservabilityOptions options)
    {
        if (!options.EnablePrometheusExporter)
        {
            return endpoints;
        }

        var path = string.IsNullOrWhiteSpace(options.PrometheusPath) ? "/metrics" : options.PrometheusPath;

        var endpoint = endpoints.MapPrometheusScrapingEndpoint(path);
        
        // Add trusted/local access filter
        endpoint.AddEndpointFilter(async (context, next) =>
        {
            var httpContext = context.HttpContext;
            var connection = httpContext.Connection;
            var remoteIp = connection.RemoteIpAddress;

            if (remoteIp is not null)
            {
                if (IPAddress.IsLoopback(remoteIp) || IsPrivateNetwork(remoteIp))
                {
                    return await next(context);
                }
            }

            // Also allow if running behind local/container network
            if (remoteIp is null)
            {
                return await next(context);
            }

            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Results.Json(new
            {
                error = "Forbidden",
                message = "Prometheus scrape endpoint is restricted to local/trusted network access."
            }, statusCode: StatusCodes.Status403Forbidden);
        });

        return endpoints;
    }

    private static bool IsPrivateNetwork(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        var bytes = ip.GetAddressBytes();
        if (bytes.Length == 4)
        {
            // 10.0.0.0/8
            if (bytes[0] == 10) return true;
            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return true;
        }

        return false;
    }
}
