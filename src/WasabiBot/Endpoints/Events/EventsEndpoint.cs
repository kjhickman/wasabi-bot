using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using WasabiBot.Core.Contracts;
using WasabiBot.Core.Models.Aws;
using WasabiBot.Interfaces;

namespace WasabiBot.Endpoints.Events;

public static class EventsEndpoint
{
    public static async Task<Ok<SqsBatchResponse>> Handle(SqsEvent sqsEvent, IInteractionService interactionService,
        ILogger logger)
    {
        var response = new SqsBatchResponse();
        await Parallel.ForEachAsync(sqsEvent.Records, async (record, _) =>
        {
            try
            {
                var interaction = JsonSerializer.Deserialize(record.Body, JsonContext.Default.Interaction);
                if (interaction is null)
                {
                    logger.Error("Interaction could not be deserialized.");
                    response.AddFailedMessageId(record.MessageId);
                    return;
                }

                await interactionService.HandleDeferredInteraction(interaction);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                response.AddFailedMessageId(record.MessageId);
            }
        });

        return TypedResults.Ok(response);
    }
}
