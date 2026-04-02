using NetCord;
using NetCord.Gateway;
using NetCord.Services.ApplicationCommands;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;

namespace WasabiBot.Api.Infrastructure.Discord.Interactions;

public sealed class WasabiCommandContext(IApplicationCommandContext inner) : ICommandContext
{
    private readonly InteractionResponder _responder = InteractionResponder.Create(inner, TimeProvider.System);
    private readonly Interaction _interaction = inner.Interaction;
    private readonly Guild? _guild = inner.Interaction.Guild;

    public ulong ChannelId => _interaction.Channel.Id;
    public ulong? GuildId => _interaction.GuildId;
    public ulong UserId => _interaction.User.Id;
    public ulong? UserVoiceChannelId => TryGetUserVoiceChannelId();
    public ulong InteractionId => _interaction.Id;
    public string Username => _interaction.User.Username;
    public string? GlobalName => _interaction.User.GlobalName;
    public string UserDisplayName =>  GlobalName ?? Username;

    public Task RespondAsync(string message, bool ephemeral = false)
    {
        return _responder.SendAsync(message, ephemeral);
    }

    public Task SendEphemeralAsync(string content)
    {
        return _responder.SendEphemeralAsync(content);
    }

    private ulong? TryGetUserVoiceChannelId()
    {
        if (_guild is null)
        {
            return null;
        }

        return _guild.VoiceStates.TryGetValue(UserId, out var voiceState)
            ? voiceState.ChannelId
            : null;
    }
}
