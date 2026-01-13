using WasabiBot.Api.Features.MagicConch;

namespace WasabiBot.Api.Infrastructure.AI;

internal static class DependencyInjection
{
    public static void AddAIServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<OpenRouterV2Options>()
            .Bind(builder.Configuration.GetSection(OpenRouterV2Options.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddScoped<IChatClientFactory, ChatClientFactory>();

        // AI Tools
        builder.Services.AddSingleton<IMagicConchTool, MagicConchTool>();
        builder.Services.AddSingleton(Random.Shared);
    }
}
