using WasabiBot.Core.Discord;

namespace WasabiBot.DataAccess.Messages;

public class InteractionDeferredMessage : Interaction
{
    public static InteractionDeferredMessage FromInteraction(Interaction interaction)
    {
        return new InteractionDeferredMessage
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
