namespace WasabiBot.Api.Features.Music;

internal sealed class NullMusicInactivityTracker : IMusicInactivityTracker
{
    public static IMusicInactivityTracker Instance { get; } = new NullMusicInactivityTracker();

    private NullMusicInactivityTracker()
    {
    }

    public void ScheduleIdleDisconnect(ulong guildId)
    {
    }

    public void SchedulePausedDisconnect(ulong guildId)
    {
    }

    public void CancelDisconnect(ulong guildId)
    {
    }
}
