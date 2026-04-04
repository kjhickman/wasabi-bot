using NetCord.Gateway;

namespace WasabiBot.Api.Features.Music;

internal sealed class SharedVoiceChannelResolver(GatewayClient gatewayClient) : ISharedVoiceChannelResolver
{
    private readonly GatewayClient _gatewayClient = gatewayClient;

    public SharedVoiceChannel? ResolveForUser(ulong userId)
    {
        var botUserId = _gatewayClient.Cache.User?.Id ?? _gatewayClient.Id;
        if (botUserId == 0)
        {
            return null;
        }

        foreach (var guild in _gatewayClient.Cache.Guilds.Values)
        {
            if (guild.IsUnavailable ||
                !guild.VoiceStates.TryGetValue(userId, out var userVoiceState) ||
                userVoiceState.ChannelId is not { } sharedChannelId ||
                !guild.VoiceStates.TryGetValue(botUserId, out var botVoiceState) ||
                botVoiceState.ChannelId != sharedChannelId)
            {
                continue;
            }

            var channelName = guild.Channels.TryGetValue(sharedChannelId, out var channel)
                ? channel.Name
                : "Unknown voice channel";

            return new SharedVoiceChannel(guild.Id, guild.Name, sharedChannelId, channelName);
        }

        return null;
    }
}
