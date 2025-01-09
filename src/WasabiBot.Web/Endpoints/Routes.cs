using WasabiBot.Web.Endpoints.Discord;
using WasabiBot.Web.Filters;

namespace WasabiBot.Web.Endpoints;

public static class Routes
{
    public static RouteGroupBuilder MapEndpoints(this RouteGroupBuilder app)
    {
        app.MapPost("/interaction", InteractionEndpoint.Handle).AddEndpointFilter<DiscordValidationFilter>();
        
        return app;
    }
}
