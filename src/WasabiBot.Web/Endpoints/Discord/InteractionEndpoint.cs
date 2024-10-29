using Microsoft.AspNetCore.Http.HttpResults;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Interfaces;

namespace WasabiBot.Web.Endpoints.Discord;

public class InteractionEndpoint
{
    public static async Task<Results<Ok<InteractionResponse>, ProblemHttpResult>> Handle(HttpContext ctx,
        IInteractionService interactionService, ILogger<InteractionEndpoint> logger)
    {
        var interaction = await ctx.Request.ReadFromJsonAsync(WebJsonContext.Default.Interaction);
        if (interaction is null)
        {
            logger.LogError("Interaction was null");
            return TypedResults.Problem();
        }
        
        var result = await interactionService.HandleInteraction(interaction);
        if (result.IsFailed)
        {
            logger.LogError("Failed to handle interaction: {Errors}", string.Join(", ", result.Errors));
            return TypedResults.Problem();
        }
        
        return TypedResults.Ok(result.Value);
    }
}
