using Amazon.SQS;
using Microsoft.Extensions.Options;
using WasabiBot.Core.Discord;
using WasabiBot.Interfaces;
using WasabiBot.Settings;

namespace WasabiBot.Commands.Handlers;

public class DeferredPingHandler : DeferredCommandHandler
{
    private readonly IDiscordService _discordService;

    public DeferredPingHandler(IAmazonSQS sqs, IOptions<EnvironmentVariables> options, IDiscordService discordService) :
        base(sqs, options)
    {
        _discordService = discordService;
    }

    public static string Name => "deferping";

    public override async Task HandleDeferredCommand(Interaction interaction)
    {
        await _discordService.CreateFollowupMessage(interaction.Token, "Deferred pong!");
    }
}
