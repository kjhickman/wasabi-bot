using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace WasabiBot.Api.Infrastructure.AI;

internal static class DependencyInjection
{
    public static void AddAIInfrastructure(this WebApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<GeminiOptions>()
            .Bind(builder.Configuration.GetSection(GeminiOptions.SectionName))
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
    }
}
