using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Microsoft.Extensions.DependencyInjection;

internal static class AI
{
    public static void AddAIServices(this WebApplicationBuilder builder)
    {
        var openAiClientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri("https://api.cerebras.ai/v1/"),
        };

        var openAiKey = builder.Configuration.GetValue<string?>("OpenAI:ApiKey") ?? throw new Exception("OpenAI API key is missing.");
        var chatClient = new OpenAIClient(new ApiKeyCredential(openAiKey), openAiClientOptions)
            .GetChatClient("gpt-oss-120b")
            .AsIChatClient();

        builder.Services.AddChatClient(chatClient)
            .UseFunctionInvocation()
            .UseOpenTelemetry()
            .UseLogging();
    }
}
