namespace WasabiBot.Api.Infrastructure.AI;

public static class AIConstants
{
    public static class Endpoints
    {
        public const string Gemini = "https://generativelanguage.googleapis.com/v1beta/openai/";
        public const string Grok = "https://api.x.ai/v1"; // XAI OpenAI-compatible endpoint
    }

    public static class Models
    {
        public const string GeminiFlashLite = "gemini-2.5-flash-lite";
        public const string GrokFastNonReasoning = "grok-4-fast-non-reasoning-latest"; // Requested model
    }
}

public enum AIServiceProvider
{
    Gemini,
    Grok
}
