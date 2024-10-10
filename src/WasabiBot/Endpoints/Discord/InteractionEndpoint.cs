using Microsoft.AspNetCore.Http.HttpResults;
using WasabiBot.Core.Discord;
using WasabiBot.Database.Entities;
using WasabiBot.Interfaces;
using WasabiBot.Services;

namespace WasabiBot.Endpoints.Discord;

public static class InteractionEndpoint
{
    public static async Task<Results<Ok<InteractionResponse>, ProblemHttpResult>> Handle(HttpContext ctx,
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
