using System.Security.Claims;
using System.Text;
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
        [FromQuery(Name = "limit")] int? limit,
        [FromQuery(Name = "sort")] string? sort,
        [FromQuery(Name = "cursor")] string? cursor,
        IInteractionService interactionService)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !long.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var sortDirection = SortDirection.Descending;
        if (sort?.ToLowerInvariant() == "asc")
        {
            sortDirection = SortDirection.Ascending;
        }

        InteractionCursor? parsedCursor = null;
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            parsedCursor = ParseCursor(cursor);
            if (parsedCursor == null)
            {
                return Results.BadRequest(new { error = "Invalid cursor format" });
            }
        }

        var pageLimit = limit ?? 100;
        if (pageLimit is < 1 or > 100)
        {
            return Results.BadRequest(new { error = "Limit must be between 1 and 100" });
        }

        var request = new GetAllInteractionsRequest
        {
            UserId = userId,
            ChannelId = channelId,
            ApplicationId = applicationId,
            GuildId = guildId,
            Limit = pageLimit,
            SortDirection = sortDirection,
            Cursor = parsedCursor
        };
        
        var entities = await interactionService.GetAllAsync(request);

        var hasMore = entities.Length > pageLimit;
        var interactions = hasMore ? entities.Take(pageLimit).ToList() : entities.ToList();

        string? nextCursor = null;
        if (hasMore && interactions.Count > 0)
        {
            var lastItem = interactions[^1];
            nextCursor = BuildCursor(lastItem.CreatedAt, lastItem.Id);
        }

        var dtos = interactions.Select(InteractionDto.FromEntity);
        var response = new GetInteractionsResponse
        {
            Interactions = dtos,
            HasMore = hasMore,
            NextCursor = nextCursor
        };
        return Results.Ok(response);
    }

    private static string BuildCursor(DateTimeOffset createdAt, long id)
    {
        var cursorData = $"{createdAt:O}|{id}";
        var bytes = Encoding.UTF8.GetBytes(cursorData);
        return Convert.ToBase64String(bytes);
    }

    private static InteractionCursor? ParseCursor(string cursor)
    {
        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var cursorData = Encoding.UTF8.GetString(bytes);
            var parts = cursorData.Split('|');

            if (parts.Length != 2)
                return null;

            if (!DateTimeOffset.TryParse(parts[0], out var createdAt))
                return null;

            if (!long.TryParse(parts[1], out var id))
                return null;

            return new InteractionCursor
            {
                CreatedAt = createdAt,
                Id = id
            };
        }
        catch
        {
            return null;
        }
    }
}
