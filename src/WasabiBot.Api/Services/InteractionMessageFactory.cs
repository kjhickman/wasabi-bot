using NetCord;
using NetCord.Rest;

namespace WasabiBot.Api.Services;

internal static class InteractionMessageFactory
{
    public static InteractionMessageProperties Create(string content, bool ephemeral = false)
    {
        return new InteractionMessageProperties
        {
            Content = content,
            Flags = ephemeral ? MessageFlags.Ephemeral : null
        };
    }
}
