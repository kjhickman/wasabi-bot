using VinceBot.Discord;
using VinceBot.Discord.Enums;

namespace VinceBot.Services;

public class InteractionService : IInteractionService
{
    private Dictionary<string, Func<Interaction, InteractionResponse>> _handlers = new();
    
    public async Task<InteractionResponse> HandleInteraction(Interaction interaction)
    {
        // var name = interaction.Data?.Name;
        // if (name is null)
        // {
        //     throw new NullReferenceException();
        // }
        // if (_handlers.TryGetValue(name, out var handler))
        // {
        //     var foo = handler(interaction);
        // }
        return new InteractionResponse
        {
            Type = InteractionResponseType.ChannelMessageWithSource,
            Data = new InteractionResponseData
            {
                MessageContent = "Pong!"
            }
        };
    }
}

public interface IInteractionService
{
    Task<InteractionResponse> HandleInteraction(Interaction interaction);
}
