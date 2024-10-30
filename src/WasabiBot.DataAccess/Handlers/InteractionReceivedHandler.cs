using FluentResults;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Extensions;
using WasabiBot.Core.Interfaces;
using WasabiBot.Core.Models.Entities;
using WasabiBot.DataAccess.Messages;
using WasabiBot.DataAccess.Services;

namespace WasabiBot.DataAccess.Handlers;

public class InteractionReceivedHandler
{
    private readonly InteractionRecordService _interactionRecordService;

    public InteractionReceivedHandler(InteractionRecordService interactionRecordService)
    {
        _interactionRecordService = interactionRecordService;
    }

    public async Task<Result> Handle(InteractionReceivedMessage message, CancellationToken ct = default)
    {
        if (message is not Interaction interaction)
        {
            return Result.Fail("Message is not interaction");
        }
    
        var result = InteractionRecord.Create(interaction);
        if (result.IsFailed)
        {
            return result.DropValue();
        }

        return await _interactionRecordService.CreateAsync(result.Value);
    }
}