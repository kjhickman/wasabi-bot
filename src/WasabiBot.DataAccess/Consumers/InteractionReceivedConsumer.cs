using MassTransit;
using OpenTelemetry.Trace;
using WasabiBot.Core.Models.Entities;
using WasabiBot.DataAccess.Messages;
using WasabiBot.DataAccess.Services;

namespace WasabiBot.DataAccess.Consumers;

public class InteractionReceivedConsumer : IConsumer<InteractionReceivedMessage>
{
    private readonly InteractionRecordService _interactionRecordService;
    private readonly Tracer _tracer;

    public InteractionReceivedConsumer(InteractionRecordService interactionRecordService, Tracer tracer)
    {
        _interactionRecordService = interactionRecordService;
        _tracer = tracer;
    }

    public async Task Consume(ConsumeContext<InteractionReceivedMessage> context)
    {
        using var span = _tracer.StartActiveSpan("consumer.interaction_received");
        var result = InteractionRecord.Create(context.Message);
        await _interactionRecordService.CreateAsync(result);
    }
}