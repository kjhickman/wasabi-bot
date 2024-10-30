using MassTransit;
using WasabiBot.Core.Models.Entities;
using WasabiBot.DataAccess.Messages;
using WasabiBot.DataAccess.Services;

namespace WasabiBot.DataAccess.Consumers;

public class InteractionReceivedConsumer : IConsumer<InteractionReceivedMessage>
{
    private readonly InteractionRecordService _interactionRecordService;

    public InteractionReceivedConsumer(InteractionRecordService interactionRecordService)
    {
        _interactionRecordService = interactionRecordService;
    }

    public async Task Consume(ConsumeContext<InteractionReceivedMessage> context)
    {
        var result = InteractionRecord.Create(context.Message);
        await _interactionRecordService.CreateAsync(result);
    }
}