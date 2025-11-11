using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using WasabiBot.DataAccess.Abstractions;
using WasabiBot.DataAccess.Services;

namespace WasabiBot.Api.Features.Interactions;

public class GetInteractions
{
    public static async Task<IResult> Handle(
        ClaimsPrincipal user,
        [FromQuery(Name = "channel_id")] long? channelId,
        [FromQuery(Name = "application_id")] long? applicationId,
        [FromQuery(Name = "guild_id")] long? guildId,
        IInteractionService interactionService)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var request = new GetAllInteractionsRequest
        {
            UserId = userId,
            ChannelId = channelId,
            ApplicationId = applicationId,
            GuildId = guildId
        };
        
        var entities = await interactionService.GetAllAsync(request);
        var dtos = entities.Select(InteractionDto.FromEntity);
        var response = new GetInteractionsResponse
        {
            Interactions = dtos
        };
        return Results.Ok(response);
    }
}
