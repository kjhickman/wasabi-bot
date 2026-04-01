using Microsoft.Extensions.AI;
using NetCord.Services.ApplicationCommands;
using OpenTelemetry.Trace;
using WasabiBot.Api.Infrastructure.AI;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Ask;

[CommandHandler("ask", "Ask a quick one-off question.")]
internal sealed class AskCommand(IChatClientFactory chatClientFactory, Tracer tracer, ILogger<AskCommand> logger)
{
    private const string SystemPrompt = "You answer one-off user questions in Discord. Keep the reply short and concise. There will be no follow-up conversation, so answer the question directly in a single response.";

    private readonly IChatClient _chatClient = chatClientFactory.GetChatClient(LlmPreset.LowLatency);
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
            var response = await _chatClient.GetResponseAsync([
                new ChatMessage(ChatRole.System, SystemPrompt),
                new ChatMessage(ChatRole.User, question)
            ]);

            _logger.LogInformation("Ask command responded to user {User}", ctx.UserDisplayName);
            await ctx.RespondAsync(response.Text);
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            _logger.LogError(ex, "Ask command failed for user {User}", ctx.UserDisplayName);
            await ctx.SendEphemeralAsync("I couldn't answer that right now. Please try again later.");
        }
    }
}
