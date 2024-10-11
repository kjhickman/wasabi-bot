using WasabiBot.Core;
using WasabiBot.Core.Discord;
using WasabiBot.Interfaces;

namespace WasabiBot.Commands.Handlers;

public class DeferredPingHandler : DeferredCommandHandler
{
    private readonly IDiscordService _discordService;

    public DeferredPingHandler(IMessageClient messageClient, IDiscordService discordService) : base(messageClient)
    {
        _discordService = discordService;
    }

    public static string Name => "deferping";

    public override async Task<Result> HandleDeferredCommand(Interaction interaction)
    {
        return await _discordService.CreateFollowupMessage(interaction.Token, "Deferred pong!");
    }
}
