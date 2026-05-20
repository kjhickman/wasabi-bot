using NetCord.Services.ApplicationCommands;
using OpenTelemetry.Trace;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Ask;

[CommandHandler("ask", "Ask a quick one-off question.")]
internal sealed class AskCommand(IAskAnswerService askAnswerService, Tracer tracer, ILogger<AskCommand> logger)
{
    private const int MaxDiscordMessageLength = 2000;
    private const string TruncatedSuffix = "\n\n_(Answer truncated to fit Discord's 2000 character limit.)_";

    private readonly IAskAnswerService _askAnswerService = askAnswerService;
    private readonly Tracer _tracer = tracer;
    private readonly ILogger<AskCommand> _logger = logger;

    public async Task ExecuteAsync(ICommandContext ctx, [SlashCommandParameter(Description = "Your question")] string question)
    {
        _logger.LogInformation(
            "Ask command invoked by user {User} in channel {ChannelId}",
            ctx.UserDisplayName,
            ctx.ChannelId);

        using var span = _tracer.StartActiveSpan("ask.answer.generate");

        try
        {
            var answer = await _askAnswerService.AnswerAsync(question);

            _logger.LogInformation("Ask command responded to user {User}", ctx.UserDisplayName);
            await ctx.RespondAsync(FormatResponse(question, answer));
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            _logger.LogError(ex, "Ask command failed for user {User}", ctx.UserDisplayName);
            await ctx.SendEphemeralAsync("I couldn't answer that right now. Please try again later.");
        }
    }

    private static string FormatResponse(string question, AskAnswer answer)
    {
        var header = $"**Question:** {question}\n\n";
        var full = header + answer.Text;
        if (full.Length <= MaxDiscordMessageLength)
        {
            return full;
        }

        var truncatedAnswerLength = Math.Max(0, MaxDiscordMessageLength - header.Length - TruncatedSuffix.Length);
        return string.Concat(header, answer.Text.AsSpan(0, truncatedAnswerLength), TruncatedSuffix);
    }
}
