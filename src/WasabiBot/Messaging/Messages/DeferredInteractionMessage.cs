using WasabiBot.Core.Discord;
using WasabiBot.Interfaces;
using WasabiBot.Messaging.Handlers;

namespace WasabiBot.Messaging.Messages;

public class DeferredInteractionMessage : Interaction, IMessage
{
    public string MessageHandlerName => nameof(InteractionMessageHandler);

    public static DeferredInteractionMessage FromInteraction(Interaction interaction)
    {
        return new DeferredInteractionMessage
        {
            Id = interaction.Id,
            ApplicationId = interaction.ApplicationId,
            Type = interaction.Type,
            Data = interaction.Data,
            GuildId = interaction.GuildId,
            ChannelId = interaction.ChannelId,
            Token = interaction.Token,
            GuildMember = interaction.GuildMember,
            User = interaction.User,
            Version = interaction.Version,
        };
    }
}
