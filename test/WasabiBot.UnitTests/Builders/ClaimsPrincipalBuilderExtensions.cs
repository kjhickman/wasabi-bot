namespace WasabiBot.UnitTests.Builders;

public static class ClaimsPrincipalBuilderExtensions
{
    /// <summary>
    /// Creates a Discord user with the required userId and username.
    /// Chain additional With* methods to add optional fields (globalName, discriminator, avatar).
    /// </summary>
    public static ClaimsPrincipalBuilder AsDiscordUser(
        this ClaimsPrincipalBuilder builder,
        string userId,
        string username)
    {
        return builder
            .WithUserId(userId)
            .WithDiscordUsername(username);
    }

    /// <summary>
    /// Creates an API user with userId and name claims (typical for API token scenarios).
    /// Chain additional With* methods to customize as needed.
    /// </summary>
    public static ClaimsPrincipalBuilder AsApiUser(
        this ClaimsPrincipalBuilder builder,
        string userId,
        string? name = null)
    {
        builder.WithUserId(userId);

        if (name is not null)
            builder.WithName(name);

        return builder;
    }

    /// <summary>
    /// Creates an unauthenticated user (no userId claim).
    /// </summary>
    public static ClaimsPrincipalBuilder AsUnauthenticatedUser(this ClaimsPrincipalBuilder builder)
    {
        // Return builder without adding userId claim
        return builder;
    }
}
