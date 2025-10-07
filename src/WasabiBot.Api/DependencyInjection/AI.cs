using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;
using WasabiBot.Api.Constants;

namespace Microsoft.Extensions.DependencyInjection;

internal static class AI
{
    public static void AddAIServices(this WebApplicationBuilder builder)
    {
        var apiKey = builder.Configuration.GetValue<string?>("Gemini:ApiKey") ?? throw new Exception("Gemini API key is missing.");
        var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(AIConstants.Endpoints.Gemini) };

        builder.Services.AddChatClient(serviceProvider =>
        {
            var chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions)
                .GetChatClient(AIConstants.Models.GeminiFlashLite)
                .AsIChatClient();

            return new ChatClientBuilder(chatClient)
                .UseOpenTelemetry(serviceProvider.GetRequiredService<ILoggerFactory>(), "Microsoft.Extensions.AI")
                .UseFunctionInvocation()
                .UseLogging()
                .Build(serviceProvider);
        });
    }
}
