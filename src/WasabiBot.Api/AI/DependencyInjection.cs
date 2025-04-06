using Microsoft.Extensions.AI;
using OpenAI;

namespace WasabiBot.Api.AI;

public static class DependencyInjection
{
    public static void AddAIServices(this WebApplicationBuilder builder)
    {
        var openAiKey = builder.Configuration.GetValue<string?>("OpenAI:ApiKey");
        builder.Services.AddChatClient(new OpenAIClient(openAiKey).GetChatClient("gpt-4o").AsIChatClient())
            .UseFunctionInvocation()
            .UseOpenTelemetry()
            .UseLogging();
    }
}
