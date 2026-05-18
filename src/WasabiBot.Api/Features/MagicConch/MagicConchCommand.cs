using Microsoft.Extensions.AI;
using NetCord.Services.ApplicationCommands;
using OpenTelemetry.Trace;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.MagicConch;

[CommandHandler("conch", "Ask the magic conch a question.")]
internal sealed class MagicConchCommand
{
    private const string UseMagicConchTool = "UseMagicConch";

    private readonly IChatClient _chatClient;
    private readonly Tracer _tracer;
    private readonly ILogger<MagicConchCommand> _logger;
    private readonly IMagicConchTool _magicConchTool;

    public MagicConchCommand(IChatClient chatClient, Tracer tracer, ILogger<MagicConchCommand> logger, IMagicConchTool magicConchTool)
    {
        _chatClient = chatClient;
        _tracer = tracer;
        _logger = logger;
        _magicConchTool = magicConchTool;
    }

    public async Task ExecuteAsync(ICommandContext ctx, [SlashCommandParameter(Description = "A yes/no question")]string question)
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
                     $"If uncertain or ambiguous, respond exactly with '{UseMagicConchTool}' (do not guess). " +
                     "Never add extra commentary, punctuation, or markdown.\n" +
                     $"Question: {question}";

        try
        {
            var chatResponse = await _chatClient.GetResponseAsync(prompt);
            var answer = chatResponse.Text.Trim();

            if (string.Equals(answer, UseMagicConchTool, StringComparison.OrdinalIgnoreCase))
            {
                answer = _magicConchTool.GetMagicConchResponse(question);
            }

            _logger.LogInformation(
                "Magic conch responded to user {User} with answer '{Answer}'",
                userDisplayName,
                answer);

            var response = $"""
                             {userDisplayName} asked: *{question}*
                             The Magic Conch says... {answer}
                             """;

            await ctx.RespondAsync(response);
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            _logger.LogWarning(ex, "Magic conch LLM call failed, falling back to tool for user {User}", userDisplayName);

            try
            {
                var fallback = _magicConchTool.GetMagicConchResponse(question);
                var response = $"""
                                 {userDisplayName} asked: *{question}*
                                 The Magic Conch says... {fallback}
                                 """;

                await ctx.RespondAsync(response);
            }
            catch (Exception fallbackEx)
            {
                span.RecordException(fallbackEx);
                _logger.LogError(fallbackEx, "Magic conch tool fallback failed for user {User}", userDisplayName);
                await ctx.SendEphemeralAsync("The magic conch is silent right now. Please try again later.");
            }
        }
    }
}
