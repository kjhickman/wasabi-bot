using WasabiBot.Api.Infrastructure.Auth.Endpoints;

namespace WasabiBot.Api.Infrastructure.Auth;

public static class Routes
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/signin-discord", DiscordSignin.Handle)
            .AllowAnonymous();

        endpoints.MapPost("/signout", (Delegate)SignOut.Handle)
            .RequireAuthorization();

        endpoints.MapGet("/token", RefreshToken.Handle)
            .RequireAuthorization("DiscordGuildMember");

        return endpoints;
    }
}
