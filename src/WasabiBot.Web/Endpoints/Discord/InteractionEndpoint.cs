using Microsoft.AspNetCore.Http.HttpResults;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Interfaces;
using WasabiBot.DataAccess.Messages;

namespace WasabiBot.Web.Endpoints.Discord;

public static class InteractionEndpoint
{
    public static async Task<Results<Ok<InteractionResponse>, ProblemHttpResult>> Handle(HttpContext ctx,
        IInteractionService interactionService, ILogger logger, IMessageClient messageClient)
    {
        var interaction = await ctx.Request.ReadFromJsonAsync(WebJsonContext.Default.Interaction);
        if (interaction is null)
        {
            logger.Error("Interaction was null.");
            return TypedResults.Problem();
        }

        var message = InteractionReceivedMessage.FromInteraction(interaction);
        var sendMessageResult = await messageClient.SendMessage(message);
        if (sendMessageResult.IsError)
        {
            logger.Error(sendMessageResult.Error, "Error sending {MessageType}", nameof(message));
        }
        
        var result = await interactionService.HandleInteraction(interaction);
        if (result.IsOk)
        {
            return TypedResults.Ok(result.Value);
        }
        
        return TypedResults.Problem();
    }
}
