using Microsoft.AspNetCore.Http.HttpResults;
using WasabiBot.Core.Interfaces;
using WasabiBot.Core.Models.Contracts;

namespace WasabiBot.Web.Endpoints.Discord;

public class RegistrationEndpoint
{
    public static async Task<Ok> Handle(RegisterCommandsRequest request, IDiscordService commandsService, 
        ILogger<RegistrationEndpoint> logger)
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
