using Discord.Interactions;
using Discord.Rest;
using Microsoft.AspNetCore.Mvc;
using WasabiBot.DataAccess.Entities;
using WasabiBot.DataAccess.Services;
using WasabiBot.Web.Services;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace WasabiBot.Web.Endpoints.Discord;

public class InteractionEndpoint
{
    public static async Task<IResult> Handle(HttpContext ctx, DiscordRestClient discord, InteractionService interactions,
        IServiceProvider provider, ILogger<InteractionEndpoint> logger)
    {
        logger.LogInformation("Received interaction");
        
        if (ctx.Items["Interaction"] is not RestInteraction interaction)
        {
            return Results.BadRequest();
        }
        
        // TODO: convert to middleware
        var sr = new StreamReader(ctx.Request.Body);
        var interactionJson = await sr.ReadToEndAsync();
        await BackgroundInteractionService.QueueInteractionAsync(interactionJson);

        if (interaction is RestPingInteraction ping)
        {
            logger.LogInformation("Received ping interaction");
            return Results.Text(ping.AcknowledgePing(), contentType: "application/json", statusCode: StatusCodes.Status200OK);
        }
        
        var tcs = new TaskCompletionSource<string>();
        var interactionCtx = new RestInteractionContext(discord, interaction, str => 
        {
            tcs.SetResult(str);
            return Task.CompletedTask;
        });
        await interactions.ExecuteCommandAsync(interactionCtx, provider);
        var response = await tcs.Task;
            
        return Results.Text(response, contentType: "application/json", statusCode: StatusCodes.Status200OK);
    }
}