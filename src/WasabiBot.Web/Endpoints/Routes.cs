using WasabiBot.Web.Endpoints.Discord;
using WasabiBot.Web.Endpoints.Events;
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
        
        app.MapPost("/events", EventsEndpoint.Handle);

        return app;
    }
}
