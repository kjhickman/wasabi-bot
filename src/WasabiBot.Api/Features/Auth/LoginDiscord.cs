using AspNet.Security.OAuth.Discord;
using Microsoft.AspNetCore.Authentication;

namespace WasabiBot.Api.Features.Auth;

public static class LoginDiscord
{
    public static IResult Handle(HttpContext context, string? returnUrl)
    {
        var redirectUri = NormalizeRedirect(context, returnUrl);

        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUri
        };

        return Results.Challenge(properties, [DiscordAuthenticationDefaults.AuthenticationScheme]);
    }

    private static string NormalizeRedirect(HttpContext context, string? returnUrl)
    {
        var pathBase = context.Request.PathBase;

        if (!string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/'))
        {
            return EnsurePathBasePrefix(pathBase, returnUrl);
        }

        return EnsurePathBasePrefix(pathBase, "/");
    }

    private static string EnsurePathBasePrefix(PathString pathBase, string path)
    {
        if (!pathBase.HasValue)
        {
            return path;
        }

        if (path.StartsWith(pathBase.Value!, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        return pathBase.Add(new PathString(path)).Value ?? path;
    }
}
