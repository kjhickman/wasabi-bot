using System.Text.Json;
using VinceBot.Contracts;
using VinceBot.Interfaces;

namespace VinceBot.Endpoints;

public static class EventModule
{
    public static WebApplication MapEventEndpoints(this WebApplication app)
    {
        app.MapPost("/events", async (SqsEvent @event, IInteractionService interactionService) =>
        {
            foreach (var record in @event.Records)
            {
                var interaction = JsonSerializer.Deserialize(record.Body, JsonContext.Default.Interaction);
                if (interaction is null)
                {
                    Console.WriteLine("Interaction could not be deserialized.");
                    continue;
                }

                await interactionService.HandleDeferredInteraction(interaction);
            }

            return TypedResults.Ok();
        });

        return app;
    }
}
