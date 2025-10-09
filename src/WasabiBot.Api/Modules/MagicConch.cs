using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NetCord.Hosting.Services.ApplicationCommands;
using OpenTelemetry.Trace;
using WasabiBot.Api.Services;

namespace WasabiBot.Api.Modules;

internal sealed class MagicConchCommand : ISlashCommand
{
    public string Name => "conch";
    public string Description => "Ask the magic conch a question.";

    private static readonly ChatOptions ChatOptions = new()
    {
        Tools = [AIFunctionFactory.Create(GetMagicConchResponse)]
    };

    public void Register(WebApplication app)
    {
        app.AddSlashCommand(Name, Description, ExecuteAsync);
    }

    private async Task ExecuteAsync(IChatClient chat, Tracer tracer, ApplicationCommandContext ctx, string question)
    {
        using var span = tracer.StartActiveSpan($"{nameof(MagicConchCommand)}.{nameof(ExecuteAsync)}");

        await using var responder = new AutoResponder(
            threshold: TimeSpan.FromMilliseconds(2300),
            defer: _ => ctx.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage()),
            respond: (text, ephemeral) => ctx.Interaction.SendResponseAsync(InteractionCallback.Message(InteractionMessageFactory.Create(text, ephemeral))),
            followup: (text, ephemeral) => ctx.Interaction.SendFollowupMessageAsync(InteractionMessageFactory.Create(text, ephemeral)));

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

    private static readonly (string Response, int Weight)[] MagicConchResponses =
    [
        ("Yes", 44),
        ("No", 32),
        ("I don't think so", 12),
        ("Maybe", 9),
        ("Try asking again", 3),
    ];
}
