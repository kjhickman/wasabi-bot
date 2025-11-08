using AspNet.Security.OAuth.Discord;
using Microsoft.AspNetCore.Authentication;

namespace WasabiBot.Api.Infrastructure.Auth.Endpoints;

public static class DiscordSignin
{
    public static IResult Handle(string? returnUrl)
    {
        var redirectUri = "/";
        if (!string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/'))
        {
            redirectUri = returnUrl;
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUri
        };

        return Results.Challenge(properties, [DiscordAuthenticationDefaults.AuthenticationScheme]);
    }
}
