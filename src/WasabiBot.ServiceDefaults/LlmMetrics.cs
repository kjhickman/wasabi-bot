using System.Diagnostics.Metrics;

namespace WasabiBot.ServiceDefaults;

/// <summary>
/// Provides custom OpenTelemetry metric instruments for AI/LLM operations.
/// </summary>
public static class LlmMetrics
{
    /// <summary>
    /// Meter name for WasabiBot AI related metrics.
    /// </summary>
    private const string MeterName = "WasabiBot.Ai";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    /// <summary>
    /// Histogram recording end-to-end latency of LLM responses (seconds).
    /// Name follows domain prefix + purpose + unit conventions.
    /// </summary>
    public static readonly Histogram<double> LlmResponseLatency =
        Meter.CreateHistogram<double>(
            name: "ai.llm.response.duration",
            unit: "s",
            description: "End-to-end latency for LLM chat responses.");
}
