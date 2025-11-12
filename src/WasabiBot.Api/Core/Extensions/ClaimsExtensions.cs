using System.Security.Claims;

namespace WasabiBot.Api.Core.Extensions;

public static class ClaimsExtensions
{
    extension(ClaimsPrincipal user)
    {
        /// <summary>
        /// Gets the display name for a Discord user from claims.
        /// Tries global name first, then username, then standard Name claim, with a fallback to "User".
        /// </summary>
        public string DisplayName => 
            user.FindFirst("urn:discord:user:global_name")?.Value
            ?? user.FindFirst("urn:discord:user:globalname")?.Value
            ?? user.FindFirst("urn:discord:user:username")?.Value
            ?? user.FindFirst(ClaimTypes.Name)?.Value
            ?? "User";

        /// <summary>
        /// Gets the Discord user ID from claims.
        /// </summary>
        public string? DiscordUserId =>
            user.FindFirst("urn:discord:user:id")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        /// <summary>
        /// Gets the Discord username from claims.
        /// </summary>
        public string? DiscordUsername =>
            user.FindFirst("urn:discord:user:username")?.Value;

        /// <summary>
        /// Gets the Discord global name from claims.
        /// </summary>
        public string? DiscordGlobalName =>
            user.FindFirst("urn:discord:user:global_name")?.Value
            ?? user.FindFirst("urn:discord:user:globalname")?.Value;
    }
}
