namespace WasabiBot.Api.Features.Music;

internal interface ISharedVoiceChannelResolver
{
    SharedVoiceChannel? ResolveForUser(ulong userId);
}
