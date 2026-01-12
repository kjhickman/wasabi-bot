using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using WasabiBot.Api.Features.MagicConch;
using Microsoft.Extensions.DependencyInjection;

namespace WasabiBot.Api.Infrastructure.AI;

internal static class DependencyInjection
{
    public static void AddAIServices(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<OpenRouterOptions>()
            .Bind(builder.Configuration.GetSection(OpenRouterOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Default chat client using the default preset
        builder.Services.AddChatClient(serviceProvider =>
        {
            return CreateOpenRouterChatClient(serviceProvider, null);
        });

        // Keyed chat clients for each preset
        builder.Services.AddKeyedSingleton<IChatClient>(AIPreset.GrokFast, (serviceProvider, _) =>
        {
            return CreateOpenRouterChatClient(serviceProvider, AIConstants.Models.GrokFast);
        });

        builder.Services.AddKeyedSingleton<IChatClient>(AIPreset.GeminiFlash, (serviceProvider, _) =>
        {
            return CreateOpenRouterChatClient(serviceProvider, AIConstants.Models.GeminiFlash);
        });

        // AI Tools
        builder.Services.AddSingleton<IMagicConchTool, MagicConchTool>();
        builder.Services.AddSingleton(Random.Shared);
    }

    private static IChatClient CreateOpenRouterChatClient(IServiceProvider serviceProvider, string? preset)
    {
        var options = serviceProvider.GetRequiredService<IOptions<OpenRouterOptions>>().Value;
        var apiKey = options.ApiKey ?? throw new InvalidOperationException("OpenRouter API key is not configured.");
        
        var modelId = preset ?? options.DefaultPreset;
        
        var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(options.Endpoint) };
        var chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions)
            .GetChatClient(modelId)
            .AsIChatClient();

        return new ChatClientBuilder(chatClient)
            .UseOpenTelemetry(serviceProvider.GetRequiredService<ILoggerFactory>(), "Microsoft.Extensions.AI")
            .UseFunctionInvocation()
            .UseLogging()
            .Build(serviceProvider);
    }
}
