using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using OpenTelemetry.Trace;

namespace WasabiBot.Api.Modules;

internal static class MagicConch
{
    public const string CommandName = "conch";
    public const string CommandDescription = "Ask the magic conch a question.";

    private static readonly ChatOptions? ChatOptions;

    static MagicConch()
    {
        var magicConchFunction = AIFunctionFactory.Create(GetMagicConchResponse);
        ChatOptions = new ChatOptions { Tools = [magicConchFunction] };
    }

    public static async Task Command(IChatClient chat, Tracer tracer, ApplicationCommandContext ctx, string question)
    {
        using var span = tracer.StartActiveSpan($"{nameof(MagicConch)}.{nameof(Command)}");

        await ctx.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        var prompt = "The user asks a yes/no question, and the magic conch shell provides a response. " +
                     "If the question is not a yes/no question, respond with 'Try asking again'. " +
                     "If you know the answer, you may only respond with Yes or No. " +
                     "If you don't know the answer, use GetMagicConchResponse()" +
                     $"The user asked: {question}";
        var response = await chat.GetResponseAsync(prompt, ChatOptions);
        await ctx.Interaction.SendFollowupMessageAsync(response.Text);
    }

    [Description("Randomly chooses a response from the magic conch shell if the answer is unknown.")]
    private static string GetMagicConchResponse()
    {
        var totalWeight = MagicConchResponses.Sum(r => r.Weight);
        var randomNumber = Random.Shared.Next(totalWeight);

        var currentWeight = 0;
        foreach (var response in MagicConchResponses)
        {
            currentWeight += response.Weight;
            if (randomNumber < currentWeight)
                return response.Response;
        }

        throw new UnreachableException("Failed to randomly choose a response.");
    }

    private static readonly List<(string Response, int Weight)> MagicConchResponses =
    [
        ("Yes", 44),
        ("No", 32),
        ("I don't think so", 12),
        ("Maybe", 9),
        ("Try asking again", 3),
    ];
}
