using WasabiBot.Core;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Discord.Enums;
using WasabiBot.Messaging.Messages;

namespace WasabiBot.Interfaces;

public abstract class DeferredCommandHandler : CommandHandler
{
    private readonly IMessageClient _messageClient;
    
    protected DeferredCommandHandler(IMessageClient messageClient)
    {
        _messageClient = messageClient;
    }

    public abstract Task<Result> HandleDeferredCommand(Interaction interaction);
    
    public override async Task<Result<InteractionResponse>> HandleCommand(Interaction interaction)
    {
        try
        {
            var message = DeferredInteractionMessage.FromInteraction(interaction);
            await _messageClient.SendMessage(message);

            return new InteractionResponse
            {
                Type = InteractionResponseType.DeferredChannelMessageWithSource
            };
        }
        catch (Exception e)
        {
            return e;
        }
        
    }
}