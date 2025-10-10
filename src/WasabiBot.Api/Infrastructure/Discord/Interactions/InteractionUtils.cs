using NetCord;
using NetCord.Rest;

namespace WasabiBot.Api.Infrastructure.Discord.Interactions;

internal static class InteractionUtils
{
    public static InteractionMessageProperties CreateMessage(string content, bool ephemeral = false)
    {
        return new InteractionMessageProperties
        {
            Content = content,
            Flags = ephemeral ? MessageFlags.Ephemeral : null
        };
    }
}
