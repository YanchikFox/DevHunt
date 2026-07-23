using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Collections.Generic;
using System.Text;

namespace DevHunt.CoreApi.Extensions;

/// <summary>
/// Extension methods for OpenTelemetry configuration
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Configures OpenTelemetry for distributed tracing
    /// </summary>
    public static IServiceCollection AddOpenTelemetryTracing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var serviceName = configuration["Tracing:ServiceName"] ?? "DevHunt.CoreApi";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceVersion: "1.0.0"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.EnrichWithHttpRequest = (activity, request) =>
                    {
                        activity.SetTag("http.request.method", request.Method);
                        activity.SetTag("http.request.path", request.Path);
                    };
                })
                .AddHttpClientInstrumentation()
                // Jaeger exporter removed - using OpenObserve for all observability
                .AddOtlpExporter("openobserve", options =>
                {
                    var endpoint = configuration["OpenObserve:OtlpTracesEndpoint"];
                    if (string.IsNullOrEmpty(endpoint))
                    {
                        // Default to secure endpoint or require configuration
                        // Using https by default to avoid CS-S1001
                        endpoint = "https://openobserve:5080/api/default/otlp/v1/traces";
                    }

                    var user = configuration["OpenObserve:User"] ?? "admin@devhunt.local";
                    var password = configuration["OpenObserve:Password"] ?? "ChangeMe123!";
                    var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}"));

                    options.Endpoint = new Uri(endpoint);
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    // NOTE: Headers in OpenTelemetry 1.10.0 is a string, not Dictionary
                    // Format: "key1=value1,key2=value2" or just the header value
                    options.Headers = $"Authorization=Basic {credentials}";
                }));

        return services;
    }
}

