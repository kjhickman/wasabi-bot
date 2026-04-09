namespace WasabiBot.Api.Features.Music;

internal interface IMusicInactivityTracker
{
    void ScheduleIdleDisconnect(ulong guildId);

    void SchedulePausedDisconnect(ulong guildId);

    void CancelDisconnect(ulong guildId);
}
