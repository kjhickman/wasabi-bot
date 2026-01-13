namespace WasabiBot.Api.Infrastructure.AI;

public static class AIConstants
{
    public static class Endpoints
    {
        public const string OpenRouter = "https://openrouter.ai/api/v1";
    }

    public static class Presets
    {
        public const string LowLatencyCreative = "@preset/grok";
        public const string LowLatency = "@preset/low-latency";
    }
}

public enum LlmPreset
{
    LowLatency,
    LowLatencyCreative
}
