using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using NetCord.Services.ApplicationCommands;
using OpenTelemetry.Trace;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.MagicConch;

internal static class MagicConchCommand
{
    public const string Name = "conch";
    public const string Description = "Ask the magic conch a question.";

    private static readonly ChatOptions ChatOptions = new()
    {
        Tools = [AIFunctionFactory.Create(GetMagicConchResponse)]
    };

    public static async Task ExecuteAsync(
        IChatClient chat,
        Tracer tracer,
        ApplicationCommandContext ctx,
        [SlashCommandParameter(Name = "question", Description = "Ask a yes/no style question")] string question)
    {
        using var span = tracer.StartActiveSpan($"{nameof(MagicConchCommand)}.{nameof(ExecuteAsync)}");
        await using var responder = InteractionResponder.Create(ctx);

        var prompt = "You are the Magic Conch shell. The user asks a yes/no style question and you reply succinctly. " +
                     "Rules: If the question is NOT yes/no, respond exactly with 'Try asking again'. " +
                     "If you confidently know, reply only 'Yes' or 'No'. " +
                     "If uncertain or ambiguous, invoke GetMagicConchResponse() (do not guess). " +
                     "Never add extra commentary, punctuation, or markdown.\n" +
                     $"Question: {question}";

        var response = await chat.GetResponseAsync(prompt, ChatOptions);
        await responder.SendAsync(response.Text);
    }

    [Description("Randomly chooses a response from the magic conch shell if the answer is unknown.")]
    private static string GetMagicConchResponse()
    {
        var randomNumber = Random.Shared.Next(TotalWeight);

        var currentWeight = 0;
        foreach (var response in MagicConchResponses)
        {
            currentWeight += response.Weight;
            if (randomNumber < currentWeight)
                return response.Response;
        }

        throw new UnreachableException("Failed to randomly choose a response.");
    }

    private static readonly (string Response, int Weight)[] MagicConchResponses =
    [
        ("Yes", 44),
        ("No", 32),
        ("I don't think so", 12),
        ("Maybe", 9),
        ("Try asking again", 3),
    ];

    private static readonly int TotalWeight = MagicConchResponses.Sum(static r => r.Weight);
}
