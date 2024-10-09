using System.Text.Json;
using WasabiBot.Contracts;
using WasabiBot.Interfaces;

namespace WasabiBot.Endpoints;

public static class EventModule
{
    public static WebApplication MapEventEndpoints(this WebApplication app)
    {
        app.MapPost("/events", async (SqsEvent @event, IInteractionService interactionService, ILogger logger, HttpContext ctx) =>
        {
            var response = new SqsBatchResponse();
            foreach (var record in @event.Records) // TODO: parallelize
            {
                try
                {
                    var interaction = JsonSerializer.Deserialize(record.Body, JsonContext.Default.Interaction);
                    if (interaction is null)
                    {
                        Console.WriteLine("Interaction could not be deserialized.");
                        response.AddFailedMessageId(record.MessageId);
                        continue;
                    }

                    await interactionService.HandleDeferredInteraction(interaction);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    response.AddFailedMessageId(record.MessageId);
                }
            }

            return TypedResults.Ok(response);
        });

        return app;
    }
}
