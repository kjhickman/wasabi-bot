using System.Security.Claims;

namespace WasabiBot.UnitTests.Builders;

public sealed class ClaimsPrincipalBuilder
{
    private readonly List<Claim> _claims = [];
    private string _authenticationType = "test";

    public static ClaimsPrincipalBuilder Create() => new();

    public ClaimsPrincipalBuilder WithClaim(string type, string value)
    {
        _claims.Add(new Claim(type, value));
        return this;
    }

    public ClaimsPrincipalBuilder WithClaim(Claim claim)
    {
        _claims.Add(claim);
        return this;
    }

    public ClaimsPrincipalBuilder WithClaims(IEnumerable<Claim> claims)
    {
        _claims.AddRange(claims);
        return this;
    }

    public ClaimsPrincipalBuilder WithUserId(string userId)
    {
        _claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        return this;
    }

    public ClaimsPrincipalBuilder WithName(string name)
    {
        _claims.Add(new Claim(ClaimTypes.Name, name));
        return this;
    }

    public ClaimsPrincipalBuilder WithDiscordUsername(string username)
    {
        _claims.Add(new Claim("urn:discord:user:username", username));
        return this;
    }

    public ClaimsPrincipalBuilder WithDiscordGlobalName(string globalName)
    {
        _claims.Add(new Claim("urn:discord:user:global_name", globalName));
        return this;
    }

    public ClaimsPrincipalBuilder WithDiscordGlobalNameAlt(string globalName)
    {
        _claims.Add(new Claim("urn:discord:user:globalname", globalName));
        return this;
    }

    public ClaimsPrincipalBuilder WithDiscordDiscriminator(string discriminator)
    {
        _claims.Add(new Claim("urn:discord:user:discriminator", discriminator));
        return this;
    }

    public ClaimsPrincipalBuilder WithDiscordAvatar(string avatarHash)
    {
        _claims.Add(new Claim("urn:discord:user:avatar", avatarHash));
        return this;
    }

    public ClaimsPrincipalBuilder WithAuthenticationType(string authenticationType)
    {
        _authenticationType = authenticationType;
        return this;
    }

    public ClaimsPrincipal Build()
    {
        var identity = new ClaimsIdentity(_claims, _authenticationType);
        return new ClaimsPrincipal(identity);
    }
}
