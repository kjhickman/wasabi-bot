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

        try
        {
            var response = await interactionService.HandleInteraction(interaction);
            return TypedResults.Ok(response);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to handle interaction");
            return TypedResults.Problem();
        }
    }
}
