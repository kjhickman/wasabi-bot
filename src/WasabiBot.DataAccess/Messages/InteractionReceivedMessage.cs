using WasabiBot.Core.Discord;
using WasabiBot.Core.Interfaces;
using WasabiBot.DataAccess.Consumers;

namespace WasabiBot.DataAccess.Messages;

public class InteractionReceivedMessage : Interaction
{
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