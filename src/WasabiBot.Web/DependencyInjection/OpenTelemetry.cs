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
                            // Filter out SQS polling traces
                            if (request.Headers.TryGetValues("X-Amz-Target", out var targetValues))
                            {
                                return !targetValues.Contains("AmazonSQS.ReceiveMessage");
                            }
                            return true;
                        };

                        options.EnrichWithHttpRequestMessage = (activity, request) =>
                        {
                            // Add basic request information
                            activity.SetTag("http.url", request.RequestUri?.ToString());
                            activity.SetTag("http.method", request.Method.ToString());
                            activity.SetTag("http.host", request.RequestUri?.Host);
                            activity.SetTag("http.path", request.RequestUri?.PathAndQuery);

                            // Add headers (be careful not to log sensitive information)
                            foreach (var header in request.Headers)
                            {
                                if (!header.Key.Contains("Authorization", StringComparison.OrdinalIgnoreCase))
                                {
                                    activity.SetTag($"http.header.{header.Key.ToLowerInvariant()}",
                                        string.Join(",", header.Value));
                                }
                            }

                            // If it's a POST request, try to capture the action
                            if (request.Content != null)
                            {
                                activity.SetTag("http.content_type", request.Content.Headers.ContentType?.MediaType);

                                // Be careful with this in production - you might want to be more selective
                                if (request.Content.Headers.ContentType?.MediaType ==
                                    "application/x-www-form-urlencoded")
                                {
                                    var content = request.Content.ReadAsStringAsync().Result;
                                    activity.SetTag("http.request.content", content);
                                }
                            }

                            // Add AWS specific information if present
                            if (request.Headers.Contains("X-Amz-Target"))
                            {
                                activity.SetTag("aws.target",
                                    request.Headers.GetValues("X-Amz-Target").FirstOrDefault());
                            }

                            // Keep these:
                            if (!request.Headers.TryGetValues("X-Amz-Target", out var targetValues)) return;

                            var operationName = targetValues.FirstOrDefault();
                            if (operationName is null)
                            {
                                return;
                            }

                            activity.DisplayName = $"{operationName}";
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