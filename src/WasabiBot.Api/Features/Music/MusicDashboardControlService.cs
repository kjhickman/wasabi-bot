using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;

namespace WasabiBot.Api.Features.Music;

internal sealed class MusicDashboardControlService(
    ISharedVoiceChannelResolver sharedVoiceChannelResolver,
    PlaybackService playbackService) : IMusicDashboardControlService
{
    private readonly ISharedVoiceChannelResolver _sharedVoiceChannelResolver = sharedVoiceChannelResolver;
    private readonly PlaybackService _playbackService = playbackService;

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
        var player = await GetControllablePlayerAsync(userId, cancellationToken);
        if (player.Result is not null)
        {
            return player.Result;
        }

        if (player.Player!.CurrentTrack is null && player.Player.Queue.Count == 0)
        {
            return new MusicCommandResult("Nothing is playing right now.", Ephemeral: true);
        }

        await player.Player.SkipAsync(cancellationToken: cancellationToken);
        return new MusicCommandResult("Skipped the current track.");
    }

    public async Task<MusicCommandResult> StopAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        var player = await GetControllablePlayerAsync(userId, cancellationToken);
        if (player.Result is not null)
        {
            return player.Result;
        }

        await player.Player!.StopAsync(cancellationToken);
        return new MusicCommandResult("Stopped playback and cleared the queue.");
    }

    private async Task<(IQueuedLavalinkPlayer? Player, MusicCommandResult? Result)> GetControllablePlayerAsync(ulong userId, CancellationToken cancellationToken)
    {
        var sharedVoiceChannel = _sharedVoiceChannelResolver.ResolveForUser(userId);
        if (sharedVoiceChannel is null)
        {
            return (null, new MusicCommandResult("You need to be in the same voice channel as the bot.", Ephemeral: true));
        }

        var player = await _playbackService.GetExistingPlayerAsync(sharedVoiceChannel.GuildId, cancellationToken);
        if (player is null)
        {
            return (null, new MusicCommandResult("I'm not connected to a voice channel right now.", Ephemeral: true));
        }

        return (player, null);
    }
}
