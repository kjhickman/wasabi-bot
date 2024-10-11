using WasabiBot.Core.Discord;
using WasabiBot.Interfaces;
using WasabiBot.Messaging.Handlers;

namespace WasabiBot.Messaging.Messages;

public class InteractionReceivedMessage : Interaction, IMessage
{
    public string MessageHandlerName => nameof(InteractionReceivedHandler);
    
    public static InteractionReceivedMessage FromInteraction(Interaction interaction)
    {
        return new InteractionReceivedMessage
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