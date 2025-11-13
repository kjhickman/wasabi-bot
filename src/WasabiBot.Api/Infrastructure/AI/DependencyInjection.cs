using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using WasabiBot.Api.Features.MagicConch;
using Microsoft.Extensions.DependencyInjection; // Added for keyed service registration

namespace WasabiBot.Api.Infrastructure.AI;

internal static class DependencyInjection
{
    public static void AddAIServices(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<GeminiOptions>()
            .Bind(builder.Configuration.GetSection(GeminiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services
            .AddOptions<GrokOptions>()
            .Bind(builder.Configuration.GetSection(GrokOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddChatClient(serviceProvider =>
        {
            var geminiOptions = serviceProvider.GetRequiredService<IOptions<GeminiOptions>>().Value;
            var apiKey = geminiOptions.ApiKey ?? throw new InvalidOperationException("Gemini API key is not configured.");

            var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(geminiOptions.Endpoint) };

            var chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions)
                .GetChatClient(geminiOptions.Model)
                .AsIChatClient();

            return new ChatClientBuilder(chatClient)
                .UseOpenTelemetry(serviceProvider.GetRequiredService<ILoggerFactory>(), "Microsoft.Extensions.AI")
                .UseFunctionInvocation()
                .UseLogging()
                .Build(serviceProvider);
        });

        builder.Services.AddKeyedSingleton<IChatClient>(AIServiceProvider.Gemini, (serviceProvider, _) =>
        {
            var geminiOptions = serviceProvider.GetRequiredService<IOptions<GeminiOptions>>().Value;
            var apiKey = geminiOptions.ApiKey ?? throw new InvalidOperationException("Gemini API key is not configured.");
            var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(geminiOptions.Endpoint) };
            var chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions)
                .GetChatClient(geminiOptions.Model)
                .AsIChatClient();
            return new ChatClientBuilder(chatClient)
                .UseOpenTelemetry(serviceProvider.GetRequiredService<ILoggerFactory>(), "Microsoft.Extensions.AI")
                .UseFunctionInvocation()
                .UseLogging()
                .Build(serviceProvider);
        });

        builder.Services.AddKeyedSingleton<IChatClient>(AIServiceProvider.Grok, (serviceProvider, _) =>
        {
            var grokOptions = serviceProvider.GetRequiredService<IOptions<GrokOptions>>().Value;
            var apiKey = grokOptions.ApiKey ?? throw new InvalidOperationException("Grok API key is not configured.");
            var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(grokOptions.Endpoint) };
            var chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions)
                .GetChatClient(grokOptions.Model)
                .AsIChatClient();
            return new ChatClientBuilder(chatClient)
                .UseOpenTelemetry(serviceProvider.GetRequiredService<ILoggerFactory>(), "Microsoft.Extensions.AI")
                .UseFunctionInvocation()
                .UseLogging()
                .Build(serviceProvider);
        });

        // AI Tools
        builder.Services.AddSingleton<IMagicConchTool, MagicConchTool>();
        builder.Services.AddSingleton(Random.Shared);
    }
}
