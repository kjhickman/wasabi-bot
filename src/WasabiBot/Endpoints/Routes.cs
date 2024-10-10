using WasabiBot.Endpoints.Discord;
using WasabiBot.Endpoints.Events;
using WasabiBot.Filters;

namespace WasabiBot.Endpoints;

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
