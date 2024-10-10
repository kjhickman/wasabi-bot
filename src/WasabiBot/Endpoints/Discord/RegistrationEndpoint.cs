using Microsoft.AspNetCore.Http.HttpResults;
using WasabiBot.Core.Contracts;
using WasabiBot.Interfaces;

namespace WasabiBot.Endpoints.Discord;

public static class RegistrationEndpoint
{
    public static async Task<Ok> Handle(RegisterCommandsRequest request, IDiscordService commandsService)
    {
        if (request.GuildId is not null)
        {
            await commandsService.RegisterGuildCommands(request.GuildId);
        }

        if (request.RegisterGlobalCommands)
        {
            await commandsService.RegisterGlobalCommands();
        }

        return TypedResults.Ok();
    }
}
