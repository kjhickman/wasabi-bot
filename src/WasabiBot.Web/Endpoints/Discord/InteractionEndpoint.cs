using Microsoft.AspNetCore.Http.HttpResults;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Interfaces;
using WasabiBot.DataAccess.Messages;

namespace WasabiBot.Web.Endpoints.Discord;

public static class InteractionEndpoint
{
    public static async Task<Results<Ok<InteractionResponse>, ProblemHttpResult>> Handle(HttpContext ctx,
        IInteractionService interactionService, ILogger logger)
    {
        var interaction = await ctx.Request.ReadFromJsonAsync(WebJsonContext.Default.Interaction);
        if (interaction is null)
        {
            logger.Error("Interaction was null");
            return TypedResults.Problem();
        }
        
        var result = await interactionService.HandleInteraction(interaction);
        if (result.IsError)
        {
            logger.Error(result.Error, "Error handling interaction");
            return TypedResults.Problem();
        }
        
        return TypedResults.Ok(result.Value);
    }
}
