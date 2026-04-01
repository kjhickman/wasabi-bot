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
            GetClaimValue(user, "urn:discord:user:global_name")
            ?? GetClaimValue(user, "urn:discord:user:globalname")
            ?? GetClaimValue(user, "urn:discord:user:username")
            ?? GetClaimValue(user, ClaimTypes.Name)
            ?? "User";

        /// <summary>
        /// Gets the Discord user ID from claims.
        /// </summary>
        public string? DiscordUserId =>
            GetClaimValue(user, "urn:discord:user:id")
            ?? GetClaimValue(user, ClaimTypes.NameIdentifier);

        /// <summary>
        /// Gets the Discord username from claims.
        /// </summary>
        public string? DiscordUsername =>
            GetClaimValue(user, "urn:discord:user:username");

        /// <summary>
        /// Gets the Discord global name from claims.
        /// </summary>
        public string? DiscordGlobalName =>
            GetClaimValue(user, "urn:discord:user:global_name")
            ?? GetClaimValue(user, "urn:discord:user:globalname");

        /// <summary>
        /// Gets the Discord discriminator from claims.
        /// </summary>
        public string? DiscordDiscriminator =>
            GetClaimValue(user, "urn:discord:user:discriminator");

        /// <summary>
        /// Gets the Discord avatar hash from claims.
        /// </summary>
        public string? DiscordAvatarHash =>
            GetClaimValue(user, "urn:discord:avatar:hash")
            ?? GetClaimValue(user, "urn:discord:user:avatar");

        /// <summary>
        /// Gets the Discord avatar URL when both user ID and avatar hash are available.
        /// </summary>
        public string? DiscordAvatarUrl =>
            GetDiscordAvatarUrl(user.DiscordUserId, user.DiscordAvatarHash, user.DiscordDiscriminator);
    }

    private static string? GetDiscordAvatarUrl(string? userId, string? avatarHash, string? discriminator)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(avatarHash))
        {
            return $"https://cdn.discordapp.com/avatars/{userId}/{avatarHash}.png?size=128";
        }

        var defaultAvatarIndex = GetDiscordDefaultAvatarIndex(userId, discriminator);
        return defaultAvatarIndex is null
            ? null
            : $"https://cdn.discordapp.com/embed/avatars/{defaultAvatarIndex.Value}.png";
    }

    private static int? GetDiscordDefaultAvatarIndex(string userId, string? discriminator)
    {
        if (!string.IsNullOrWhiteSpace(discriminator)
            && discriminator is not "0"
            && discriminator is not "0000"
            && int.TryParse(discriminator, out var legacyDiscriminator))
        {
            return legacyDiscriminator % 5;
        }

        if (!ulong.TryParse(userId, out var snowflake))
        {
            return null;
        }

        return (int)((snowflake >> 22) % 6);
    }

    private static string? GetClaimValue(ClaimsPrincipal user, string claimType)
    {
        var value = user.FindFirst(claimType)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
