using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Microsoft.Extensions.DependencyInjection;

internal static class AI
{
    public static void AddAIServices(this WebApplicationBuilder builder)
    {
        var openAiKey = builder.Configuration.GetValue<string?>("OpenAI:ApiKey") ?? throw new Exception("OpenAI API key is missing.");

        builder.Services.AddChatClient(serviceProvider =>
        {
            var chatClient = new OpenAIClient(new ApiKeyCredential(openAiKey))
                .GetChatClient("gpt-5-mini")
                .AsIChatClient();

            return new ChatClientBuilder(chatClient)
                .UseOpenTelemetry(serviceProvider.GetRequiredService<ILoggerFactory>(), "Microsoft.Extensions.AI")
                .UseFunctionInvocation()
                .UseLogging()
                .Build(serviceProvider);
        });
    }
}
