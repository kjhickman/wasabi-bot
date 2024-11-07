using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Hosting;

public static class OpenTelemetry
{
    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole();
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource("wasabi_bot");
                tracing.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.FilterHttpRequestMessage = request =>
                        {
                            // Check if this is an SQS request
                            if (request.RequestUri?.Host.Contains("sqs") != true) return true;
                            
                            // Check if this is a polling request (usually a ReceiveMessage action)
                            var isPolling = request.RequestUri.Query.Contains("Action=ReceiveMessage") ||
                                            (request.Content is { Headers.ContentType.MediaType: "application/x-www-form-urlencoded" } && 
                                             request.Content.ReadAsStringAsync().Result.Contains("Action=ReceiveMessage"));
                            
                            // Return false to filter out polling requests
                            return !isPolling;

                            // Include all other requests
                        };
                    });
            });
        
        builder.AddOpenTelemetryExporters();
        builder.Services.AddSingleton(TracerProvider.Default.GetTracer("wasabi_bot"));

        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }
}