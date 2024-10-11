using WasabiBot.Core;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Extensions;
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
        if (message is not Interaction interaction)
        {
            return Result.Fail("Message is not interaction");
        }
    
        var result = InteractionRecord.Create(interaction);
        if (result.IsError)
        {
            return result.DropValue();
        }

        return await _interactionRecordService.CreateAsync(result.Value);
    }
}