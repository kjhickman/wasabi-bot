using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;

namespace WasabiBot.Api.Features.Music;

internal sealed class MusicDashboardControlService(
    ISharedVoiceChannelResolver sharedVoiceChannelResolver,
    PlaybackService playbackService,
    IMusicQueueMutationCoordinator queueMutationCoordinator) : IMusicDashboardControlService
{
    private readonly ISharedVoiceChannelResolver _sharedVoiceChannelResolver = sharedVoiceChannelResolver;
    private readonly PlaybackService _playbackService = playbackService;
    private readonly IMusicQueueMutationCoordinator _queueMutationCoordinator = queueMutationCoordinator;

    public async Task<MusicCommandResult> TogglePauseAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var player = await GetControllablePlayerAsync(userId, cancellationToken);
        if (player.Result is not null)
        {
            return player.Result;
        }

        if (player.Player!.CurrentTrack is null)
        {
            return new MusicCommandResult("Nothing is playing right now.", Ephemeral: true);
        }

        if (player.Player.State == PlayerState.Paused)
        {
            await player.Player.ResumeAsync(cancellationToken);
            return new MusicCommandResult("Resumed playback.");
        }

        await player.Player.PauseAsync(cancellationToken);
        return new MusicCommandResult("Paused playback.");
    }

    public async Task<MusicCommandResult> SkipAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var sharedVoiceChannel = _sharedVoiceChannelResolver.ResolveForUser(userId);
        if (sharedVoiceChannel is null)
        {
            return new MusicCommandResult("You need to be in the same voice channel as the bot.", Ephemeral: true);
        }

        return await _queueMutationCoordinator.ExecuteAsync(sharedVoiceChannel.GuildId, async ct =>
        {
            var player = await _playbackService.GetExistingPlayerAsync(sharedVoiceChannel.GuildId, ct);
            if (player is null)
            {
                return new MusicCommandResult("I'm not connected to a voice channel right now.", Ephemeral: true);
            }

            if (player.CurrentTrack is null && player.Queue.Count == 0)
            {
                return new MusicCommandResult("Nothing is playing right now.", Ephemeral: true);
            }

            await player.SkipAsync(cancellationToken: ct);
            return new MusicCommandResult("Skipped the current track.");
        }, cancellationToken);
    }

    public async Task<MusicCommandResult> StopAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var sharedVoiceChannel = _sharedVoiceChannelResolver.ResolveForUser(userId);
        if (sharedVoiceChannel is null)
        {
            return new MusicCommandResult("You need to be in the same voice channel as the bot.", Ephemeral: true);
        }

        return await _queueMutationCoordinator.ExecuteAsync(sharedVoiceChannel.GuildId, async ct =>
        {
            var player = await _playbackService.GetExistingPlayerAsync(sharedVoiceChannel.GuildId, ct);
            if (player is null)
            {
                return new MusicCommandResult("I'm not connected to a voice channel right now.", Ephemeral: true);
            }

            await player.StopAsync(ct);
            return new MusicCommandResult("Stopped playback and cleared the queue.");
        }, cancellationToken);
    }

    private async Task<(SharedVoiceChannel? SharedVoiceChannel, IQueuedLavalinkPlayer? Player, MusicCommandResult? Result)> GetControllablePlayerAsync(ulong userId, CancellationToken cancellationToken)
    {
        var sharedVoiceChannel = _sharedVoiceChannelResolver.ResolveForUser(userId);
        if (sharedVoiceChannel is null)
        {
            return (null, null, new MusicCommandResult("You need to be in the same voice channel as the bot.", Ephemeral: true));
        }

        var player = await _playbackService.GetExistingPlayerAsync(sharedVoiceChannel.GuildId, cancellationToken);
        if (player is null)
        {
            return (null, null, new MusicCommandResult("I'm not connected to a voice channel right now.", Ephemeral: true));
        }

        return (sharedVoiceChannel, player, null);
    }
}
