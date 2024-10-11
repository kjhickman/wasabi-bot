using WasabiBot.Core;
using WasabiBot.Core.Discord;
using WasabiBot.Database.Entities;
using WasabiBot.Interfaces;
using WasabiBot.Messaging.Messages;
using WasabiBot.Services;

namespace WasabiBot.Messaging.Handlers;

public class InteractionReceivedHandler : IMessageHandler<InteractionReceivedMessage>
{
    private readonly InteractionRecordService _interactionRecordService;

    public InteractionReceivedHandler(InteractionRecordService interactionRecordService)
    {
        _interactionRecordService = interactionRecordService;
    }

    public async Task<Result> Handle(IMessage message, CancellationToken ct = default)
    {
        try
        {
            if (message is not Interaction interaction)
            {
                return Result.Fail("Message is not interaction");
            }
        
            var record = InteractionRecord.Create(interaction);

            return await _interactionRecordService.CreateAsync(record);
        }
        catch (Exception e)
        {
            return e;
        }
    }
}