using WasabiBot.Core.Discord;
using WasabiBot.Core.Interfaces;
using WasabiBot.Core.Models;

namespace WasabiBot.Web.Commands.Handlers;

public class DeferredPingHandlerBase : DeferredCommandHandlerBase
{
    private readonly IDiscordService _discordService;

    public DeferredPingHandlerBase(IMessageClient messageClient, IDiscordService discordService) : base(messageClient)
    {
        _discordService = discordService;
    }

    public static string Name => "deferping";

    public override async Task<Result> HandleDeferredCommand(Interaction interaction)
    {
        return await _discordService.CreateFollowupMessage(interaction.Token, "Deferred pong!");
    }
}
