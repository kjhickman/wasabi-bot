using NetCord;
using NetCord.Services.ApplicationCommands;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;

namespace WasabiBot.Api.Infrastructure.Discord.Interactions;

public sealed class DiscordCommandContext : ICommandContext
{
    private readonly InteractionResponder _responder;
    private readonly Interaction _interaction;

    public DiscordCommandContext(ApplicationCommandContext inner)
    {
        _responder = InteractionResponder.Create(inner);
        _interaction = inner.Interaction;
    }

    public ulong ChannelId => _interaction.Channel.Id;
    public ulong UserId => _interaction.User.Id;
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
}
