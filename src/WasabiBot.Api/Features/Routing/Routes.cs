using System.Security.Claims;
using WasabiBot.Api.Features.Auth;
using WasabiBot.Api.Features.Credentials;
using WasabiBot.Api.Features.Interactions;
using WasabiBot.Api.Features.OAuth;
using WasabiBot.Api.Infrastructure.Auth;

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

        app.MapPost("/oauth/token", GetOAuthToken.Handle)
            .WithDisplayName("OAuth Token")
            .WithDescription("Exchanges API client credentials for a short-lived access token.")
            .AllowAnonymous()
            .DisableAntiforgery()
            .Produces<TokenResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("OAuth");

        // API v1 endpoints
        var v1 = app.MapGroup("/api/v1")
            .WithTags("API v1");

        var credentials = v1.MapGroup("/creds")
            .WithTags("Credentials")
            .RequireAuthorization("DiscordGuildMember");

        credentials.MapGet("/", ListCredentials.Handle)
            .WithDisplayName("List Credentials")
            .WithDescription("Lists API credentials for the authenticated user.")
            .Produces<CredentialResponse[]>();

        credentials.MapPost("/", CreateCredential.Handle)
            .WithDisplayName("Create Credential")
            .WithDescription("Creates a new API credential for the authenticated user.")
            .Produces<CredentialIssueResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        credentials.MapDelete("/{id:long}", DeleteCredential.Handle)
            .WithDisplayName("Delete Credential")
            .WithDescription("Revokes an API credential owned by the authenticated user.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        credentials.MapPost("/{id:long}/regenerate-secret", RegenerateCredentialSecret.Handle)
            .WithDisplayName("Regenerate Credential Secret")
            .WithDescription("Regenerates the secret for an API credential owned by the authenticated user.")
            .Produces<CredentialIssueResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        var interactions = v1.MapGroup("/interactions")
            .WithTags("Interactions");

        interactions.MapGet("/", GetInteractions.Handle)
            .WithDisplayName("Get Interactions")
            .WithDescription("Retrieves a list of interactions.")
            .RequireAuthorization("DiscordGuildMember")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .Produces<GetInteractionsResponse>();

        interactions.MapGet("/{id:long}", GetInteractionById.Handle)
            .WithDisplayName("Get Interaction by ID")
            .WithDescription("Retrieves an interaction by its unique ID.")
            .RequireAuthorization("DiscordGuildMember")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .Produces<InteractionDto>();

        return app;
    }
}
