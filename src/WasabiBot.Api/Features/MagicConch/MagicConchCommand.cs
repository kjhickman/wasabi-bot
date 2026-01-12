using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using NetCord.Services.ApplicationCommands;
using OpenTelemetry.Trace;
using WasabiBot.Api.Infrastructure.AI;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.MagicConch;

[CommandHandler("conch", "Ask the magic conch a question.")]
internal sealed class MagicConchCommand
{
    private readonly IChatClient _chatClient;
    private readonly Tracer _tracer;
    private readonly ILogger<MagicConchCommand> _logger;
    private readonly IMagicConchTool _magicConchTool;

    public MagicConchCommand([FromKeyedServices(AIPreset.GrokFast)] IChatClient chatClient, Tracer tracer, ILogger<MagicConchCommand> logger, IMagicConchTool magicConchTool)
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
                     "If uncertain or ambiguous, invoke GetMagicConchResponse() (do not guess). " +
                     "Never add extra commentary, punctuation, or markdown.\n" +
                     $"Question: {question}";

        try
        {
            var chatOptions = new ChatOptions
            {
                Tools = [AIFunctionFactory.Create(_magicConchTool.GetMagicConchResponse)]
            };
            var chatResponse = await _chatClient.GetResponseAsync(prompt, chatOptions);
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
}
