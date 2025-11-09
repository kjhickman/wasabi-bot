using WasabiBot.Api.Features.Auth;
using WasabiBot.Api.Features.Token;

namespace WasabiBot.Api.Features.Routing;

public static class Routes
{
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        // Auth endpoints
        var auth = app.MapGroup("/auth")
            .WithTags("Authentication");

        auth.MapGet("/login-discord", LoginDiscord.Handle)
            .WithDisplayName("Discord Login")
            .WithDescription("Initiates the Discord OAuth2 login process.")
            .AllowAnonymous();

        auth.MapGet("/me", GetCurrentUser.Handle)
            .WithDisplayName("Current User")
            .WithDescription("Returns details about the authenticated Discord user.")
            .Produces<CurrentUserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();

        auth.MapPost("/logout", (Delegate)Logout.Handle)
            .WithDisplayName("Logout")
            .WithDescription("Logs the user out of the application.")
            .RequireAuthorization();

        // API v1 endpoints
        var v1 = app.MapGroup("/api/v1")
            .WithTags("API v1");

        v1.MapGet("/token", GetToken.Handle)
            .WithDisplayName("API Token")
            .WithDescription("Generates a new API token for the authenticated user.")
            .RequireAuthorization("DiscordGuildMember")
            .Produces<TokenResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}
