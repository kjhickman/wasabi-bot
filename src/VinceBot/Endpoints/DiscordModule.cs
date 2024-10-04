using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VinceBot.Contracts;
using VinceBot.Discord;
using VinceBot.Discord.Enums;
using VinceBot.Filters;
using VinceBot.Services;
using VinceBot.Settings;

namespace VinceBot.Endpoints;

public static class DiscordModule
{
    public static WebApplication MapDiscordEndpoints(this WebApplication app)
    {
        var discordGroup = app.MapGroup("/discord");
        discordGroup.MapPost("/interaction", HandleInteraction)
            .AddEndpointFilter<DiscordValidationFilter>();

        discordGroup.MapPost("/register", async (RegisterCommandsRequest request, ICommandsService commandsService) =>
        {
            if (request.GuildId is not null)
            {
                await commandsService.RegisterGuildCommands(request.GuildId);
            }

            if (request.RegisterGlobalCommands)
            {
                await commandsService.RegisterGlobalCommands();
            }
            
            return TypedResults.Ok("Successfully registered commands!");
        });

        return app;
    }

    public static async Task<Results<Ok<InteractionResponse>, ProblemHttpResult>> HandleInteraction(HttpContext ctx,
        IInteractionService interactionService)
    {
        var interaction = await ctx.Request.ReadFromJsonAsync(JsonContext.Default.Interaction);
        if (interaction == null)
        {
            Console.WriteLine("Interaction could not be deserialized.");
            return TypedResults.Problem();
        }

        if (interaction.Type == InteractionType.Ping)
        {
            // ACK ping
            return TypedResults.Ok(InteractionResponse.Pong());
        }

        var response = await interactionService.HandleInteraction(interaction);

        return TypedResults.Ok(response);
    }
}
