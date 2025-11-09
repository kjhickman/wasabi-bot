using System.Security.Claims;

namespace WasabiBot.Api.Features.Auth;

public static class GetCurrentUser
{
    public static IResult Handle(ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        var username = user.FindFirst("urn:discord:user:username")?.Value
            ?? user.FindFirst(ClaimTypes.Name)?.Value;
        var globalName = user.FindFirst("urn:discord:user:global_name")?.Value
            ?? user.FindFirst("urn:discord:user:globalname")?.Value;
        var discriminator = user.FindFirst("urn:discord:user:discriminator")?.Value;
        var avatarHash = user.FindFirst("urn:discord:user:avatar")?.Value;

        string? avatarUrl = null;
        if (!string.IsNullOrWhiteSpace(avatarHash))
        {
            avatarUrl = $"https://cdn.discordapp.com/avatars/{userId}/{avatarHash}.png?size=128";
        }

        var response = new CurrentUserResponse(
            UserId: userId,
            Username: username,
            GlobalName: globalName,
            Discriminator: discriminator,
            AvatarUrl: avatarUrl
        );

        return Results.Ok(response);
    }
}
