namespace WasabiBot.Api.Infrastructure.AI;

public static class AIConstants
{
    public static class Endpoints
    {
        public const string OpenRouter = "https://openrouter.ai/api/v1";
    }

    public static class Models
    {
        public const string GrokFast = "@preset/grok";
        public const string GeminiFlash = "@preset/gemini-flash";
    }
}

public enum AIPreset
{
    GrokFast,
    GeminiFlash
}
