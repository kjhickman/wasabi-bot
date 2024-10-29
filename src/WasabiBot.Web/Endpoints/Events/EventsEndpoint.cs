namespace WasabiBot.Web.Endpoints.Events;

public static class EventsEndpoint
{
    // public static async Task<Ok<SqsBatchResponse>> Handle(SqsEvent sqsEvent, MessageHandlerRouter router, ILogger logger)
    // {
    //     var response = new SqsBatchResponse();
    //     await Parallel.ForEachAsync(sqsEvent.Records, async (record, ct) =>
    //     {
    //         try
    //         {
    //             if (!record.MessageAttributes.TryGetValue("MessageHandler", out var messageAttribute))
    //             {
    //                 logger.Error("No attribute for MessageHandler");
    //                 response.AddFailedMessageId(record.MessageId);
    //                 return;
    //             }
    //
    //             var result = await router.Route(record.Body, messageAttribute.StringValue, ct);
    //             if (result.IsError)
    //             {
    //                 logger.Error(result.Error, "Error while processing message");
    //                 response.AddFailedMessageId(record.MessageId);
    //             }
    //         }
    //         catch (Exception e)
    //         {
    //             logger.Error(e, "An exception occurred while handling event");
    //             response.AddFailedMessageId(record.MessageId);
    //         }
    //     });
    //
    //     return TypedResults.Ok(response);
    // }
}