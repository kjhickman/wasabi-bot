using WasabiBot.Api.Features.MagicConch;

namespace WasabiBot.Api.Infrastructure.AI;

internal static class DependencyInjection
{
    public static void AddAIServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<OpenRouterV2Options>()
            .Bind(builder.Configuration.GetSection(OpenRouterV2Options.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "OpenRouterV2:ApiKey is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.LowLatencyPreset), "OpenRouterV2:LowLatencyPreset is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.LowLatencyCreativePreset), "OpenRouterV2:LowLatencyCreativePreset is required.")
            .ValidateOnStart();

        builder.Services.AddScoped<IChatClientFactory, ChatClientFactory>();

        // AI Tools
        builder.Services.AddSingleton<IMagicConchTool, MagicConchTool>();
        builder.Services.AddSingleton(Random.Shared);
    }
}
