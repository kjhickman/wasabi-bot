using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using WasabiBot.Api.Features.Music;

namespace WasabiBot.Api.Infrastructure.Discord.EventHandlers;

internal sealed class VoiceStateUpdatedEventHandler(
    GatewayClient gatewayClient,
    IServiceProvider serviceProvider,
    ILogger<VoiceStateUpdatedEventHandler> logger) : IVoiceStateUpdateGatewayHandler
{
    private readonly GatewayClient _gatewayClient = gatewayClient;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<VoiceStateUpdatedEventHandler> _logger = logger;

    public async ValueTask HandleAsync(VoiceState arg)
    {
        // NetCord updates its voice-state cache after handlers return, so defer the alone check.
        await Task.Yield();
        _ = DisconnectIfBotIsAloneAsync(arg.GuildId);
    }

    private async Task DisconnectIfBotIsAloneAsync(ulong guildId)
    {
        try
        {
            await Task.Delay(100);
            var botUserId = _gatewayClient.Cache.User?.Id ?? _gatewayClient.Id;
            if (botUserId == 0 ||
                !TryGetBotVoiceChannelId(guildId, botUserId, out var botChannelId) ||
                HasOtherUserInChannel(guildId, botUserId, botChannelId))
            {
                return;
            }

            await using var scope = _serviceProvider.CreateAsyncScope();
            var playbackService = scope.ServiceProvider.GetRequiredService<PlaybackService>();
            var queueMutationCoordinator = scope.ServiceProvider.GetRequiredService<IMusicQueueMutationCoordinator>();

            await queueMutationCoordinator.ExecuteAsync(guildId, async ct =>
            {
                if (!TryGetBotVoiceChannelId(guildId, botUserId, out var currentBotChannelId) ||
                    currentBotChannelId != botChannelId ||
                    HasOtherUserInChannel(guildId, botUserId, botChannelId))
                {
                    return false;
                }

                var player = await playbackService.GetExistingPlayerAsync(guildId, ct);
                if (player is null)
                {
                    return false;
                }

                await player.StopAsync(ct);
                await player.DisconnectAsync(ct);
                _logger.LogInformation("Disconnected music session because the bot was alone in voice channel {ChannelId} for guild {GuildId}", botChannelId, guildId);
                return true;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process music voice-state update for guild {GuildId}", guildId);
        }
    }

    private bool TryGetBotVoiceChannelId(ulong guildId, ulong botUserId, out ulong channelId)
    {
        if (_gatewayClient.Cache.Guilds.TryGetValue(guildId, out var guild) &&
            !guild.IsUnavailable &&
            guild.VoiceStates.TryGetValue(botUserId, out var botVoiceState) &&
            botVoiceState.ChannelId is { } botChannelId)
        {
            channelId = botChannelId;
            return true;
        }

        channelId = 0;
        return false;
    }

    private bool HasOtherUserInChannel(ulong guildId, ulong botUserId, ulong channelId)
    {
        if (!_gatewayClient.Cache.Guilds.TryGetValue(guildId, out var guild) || guild.IsUnavailable)
        {
            return false;
        }

        return guild.VoiceStates.Values.Any(voiceState => voiceState.UserId != botUserId && voiceState.ChannelId == channelId);
    }
}
