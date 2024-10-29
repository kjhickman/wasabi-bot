using FluentResults;
using Microsoft.AspNetCore.Http.HttpResults;
using WasabiBot.Core.Interfaces;
using WasabiBot.Core.Models.Contracts;

namespace WasabiBot.Web.Endpoints.Discord;

public static class RegistrationEndpoint
{
    public static async Task<Ok> Handle(RegisterCommandsRequest request, IDiscordService commandsService, ILogger logger)
    {
        Result? result;
        if (request.GuildId is not null)
        {
            result = await commandsService.RegisterGuildCommands(request.GuildId);
            if (result.IsFailed)
            {
                logger.Error("Failed to register guild commands: {Errors}", string.Join(", ", result.Errors));
            }
        }

        if (request.RegisterGlobalCommands)
        {
            result = await commandsService.RegisterGlobalCommands();
            if (result.IsFailed)
            {
                logger.Error("Failed to register global commands: {Errors}", string.Join(", ", result.Errors));
            }
        }

        return TypedResults.Ok();
    }
}
