using Microsoft.AspNetCore.Http.HttpResults;
using WasabiBot.Core;
using WasabiBot.Core.Contracts;
using WasabiBot.Interfaces;

namespace WasabiBot.Endpoints.Discord;

public static class RegistrationEndpoint
{
    public static async Task<Ok> Handle(RegisterCommandsRequest request, IDiscordService commandsService, ILogger logger)
    {
        Result? result;
        if (request.GuildId is not null)
        {
            result = await commandsService.RegisterGuildCommands(request.GuildId);
            if (result.IsError)
            {
                logger.Error(result.Error, "Failed to register guild commands.");
            }
        }

        if (request.RegisterGlobalCommands)
        {
            result = await commandsService.RegisterGlobalCommands();
            if (result.IsError)
            {
                logger.Error(result.Error, "Failed to register global commands.");
            }
        }

        return TypedResults.Ok();
    }
}
