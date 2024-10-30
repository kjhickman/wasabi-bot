using WasabiBot.Web.Endpoints.Discord;
using WasabiBot.Web.Filters;

namespace WasabiBot.Web.Endpoints;

public static class Routes
{
    public static RouteGroupBuilder MapEndpoints(this RouteGroupBuilder app)
    {
        var discordGroup = app.MapGroup("/discord");
        discordGroup.MapPost("/interaction", InteractionEndpoint.Handle)
            .AddEndpointFilter<DiscordValidationFilter>();
        discordGroup.MapPost("/register", RegistrationEndpoint.Handle);
        
        return app;
    }
}
