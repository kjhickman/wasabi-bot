using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using NetCord.Services.ApplicationCommands;
using OpenTelemetry.Trace;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;
using WasabiBot.ServiceDefaults;

namespace WasabiBot.Api.Features.MagicConch;

internal sealed class MagicConchCommand : CommandBase
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

    public override string Command => "conch";
    public override string Description => "Ask the magic conch a question.";

    [CommandEntry]
    public Task HandleAsync(
        ApplicationCommandContext ctx,
        [SlashCommandParameter(Name = "question", Description = "Ask a yes/no style question")] string question)
    {
        var commandContext = new DiscordCommandContext(ctx);
        return ExecuteAsync(commandContext, question);
    }

    public async Task ExecuteAsync(ICommandContext ctx, string question)
    {
        var user = ctx.Interaction.User;
        var userDisplayName = user.GlobalName ?? user.Username;
        var channelId = ctx.Interaction.Channel.Id;

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
            var llmStart = Stopwatch.GetTimestamp();
            var chatResponse = await _chatClient.GetResponseAsync(prompt, ChatOptions);
            var elapsed = Stopwatch.GetElapsedTime(llmStart).TotalSeconds;
            LlmMetrics.LlmResponseLatency.Record(elapsed, new TagList
            {
                {"command", Command},
                {"status", "ok"}
            });
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
