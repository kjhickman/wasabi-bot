using Microsoft.AspNetCore.Http.HttpResults;
using WasabiBot.Contracts;
using WasabiBot.Database.Entities;
using WasabiBot.Discord;
using WasabiBot.Filters;
using WasabiBot.Interfaces;
using WasabiBot.Services;

namespace WasabiBot.Endpoints;

public static class DiscordModule
{
    public static WebApplication MapDiscordEndpoints(this WebApplication app)
    {
        var discordGroup = app.MapGroup("/discord");
        discordGroup.MapPost("/interaction", HandleInteraction)
            .AddEndpointFilter<DiscordValidationFilter>();

        discordGroup.MapPost("/register", async (RegisterCommandsRequest request, IDiscordService commandsService) =>
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
        IInteractionService interactionService, ILogger logger, InteractionRecordService repo)
    {
        var interaction = await ctx.Request.ReadFromJsonAsync(JsonContext.Default.Interaction);
        if (interaction == null)
        {
            return TypedResults.Problem();
        }

        try
        {
            var interactionRecord = InteractionRecord.Create(interaction);
            await repo.CreateAsync(interactionRecord);
        }
        catch (Exception e)
        {
            logger.Error(e, "Failed to save the interaction.");
        }
        
        var response = await interactionService.HandleInteraction(interaction);

        return TypedResults.Ok(response);
    }
}
