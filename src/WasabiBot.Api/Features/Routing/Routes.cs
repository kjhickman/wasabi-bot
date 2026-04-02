using WasabiBot.Api.Features.Auth;
using WasabiBot.Api.Features.Interactions;
using WasabiBot.Api.Features.OAuth;

namespace WasabiBot.Api.Features.Routing;

public static class Routes
{
    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        // Auth endpoints
        var auth = app.MapGroup("/auth")
            .ExcludeFromDescription();

        auth.MapGet("/login-discord", LoginDiscord.Handle)
            .WithDisplayName("Discord Login")
            .WithDescription("Initiates the Discord OAuth2 login process.")
            .AllowAnonymous();

        auth.MapPost("/logout", (Delegate)Logout.Handle)
            .WithDisplayName("Logout")
            .WithDescription("Logs the user out of the application.")
            .RequireAuthorization();

        // API v1 endpoints
        var v1 = app.MapGroup("/api/v1")
            .WithTags("API v1");

        var oauth = v1.MapGroup("/oauth")
            .WithTags("OAuth");

        oauth.MapPost("/token", GetOAuthToken.Handle)
            .WithDisplayName("OAuth Token")
            .WithDescription("Exchanges API client credentials for a short-lived access token.")
            .AllowAnonymous()
            .DisableAntiforgery()
            .Produces<TokenResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        var interactions = v1.MapGroup("/interactions")
            .WithTags("Interactions");

        interactions.MapGet("/", GetInteractions.Handle)
            .WithDisplayName("Get Interactions")
            .WithDescription("Retrieves a list of interactions.")
            .RequireAuthorization("ApiToken")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .Produces<GetInteractionsResponse>();

        interactions.MapGet("/{id:long}", GetInteractionById.Handle)
            .WithDisplayName("Get Interaction by ID")
            .WithDescription("Retrieves an interaction by its unique ID.")
            .RequireAuthorization("ApiToken")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces<InteractionDto>();

        return app;
    }
}
