using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.MagicConch;

[CommandHandler("conch", "Ask the magic conch a question.", nameof(ExecuteAsync))]
internal sealed class MagicConchCommand
{
    private readonly IChatClient _chatClient;
    private readonly Tracer _tracer;
    private readonly ILogger<MagicConchCommand> _logger;

    private static readonly ChatOptions ChatOptions = new()
    {
        Tools = [AIFunctionFactory.Create(GetMagicConchResponse)]
    };

    public MagicConchCommand(IChatClient chatClient, Tracer tracer, ILogger<MagicConchCommand> logger)
    {
        _chatClient = chatClient;
        _tracer = tracer;
        _logger = logger;
    }

    public async Task ExecuteAsync(ICommandContext ctx, string question)
    {
        var userDisplayName = ctx.UserDisplayName;
        var channelId = ctx.ChannelId;

        _logger.LogInformation(
            "Magic conch command invoked by user {User} in channel {ChannelId}",
            userDisplayName,
            channelId);

        using var span = _tracer.StartActiveSpan("conch.answer.generate");

        var prompt = "You are the Magic Conch shell. The user asks a yes/no style question and you reply succinctly. " +
                     "Rules: If the question is NOT yes/no, respond exactly with 'Try asking again'. " +
                     "If you confidently know, reply only 'Yes' or 'No'. " +
                     "If uncertain or ambiguous, invoke GetMagicConchResponse() (do not guess). " +
                     "Never add extra commentary, punctuation, or markdown.\n" +
                     $"Question: {question}";

        try
        {
            var chatResponse = await _chatClient.GetResponseAsync(prompt, ChatOptions);
            _logger.LogInformation(
                "Magic conch responded to user {User} with answer '{Answer}'",
                userDisplayName,
                chatResponse.Text);

            var response = $"""
                             {userDisplayName} asked: *{question}*
                             The Magic Conch says... {chatResponse.Text}
                             """;

            await ctx.RespondAsync(response);
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            _logger.LogError(
                ex,
                "Magic conch failed to process question for user {User}",
                userDisplayName);
            await ctx.SendEphemeralAsync("The magic conch is silent right now. Please try again later.");
        }
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
