using NetCord;
using NetCord.Services.ApplicationCommands;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;

namespace WasabiBot.Api.Infrastructure.Discord.Interactions;

public sealed class DiscordCommandContext : ICommandContext
{
    private readonly InteractionResponder _responder;

    public DiscordCommandContext(ApplicationCommandContext inner)
    {
        _responder = InteractionResponder.Create(inner);
        Interaction = inner.Interaction;
    }

    public Interaction Interaction { get; }

    public Task RespondAsync(string message, bool ephemeral = false)
    {
        return _responder.SendAsync(message, ephemeral);
    }

    public Task SendEphemeralAsync(string content)
    {
        return _responder.SendEphemeralAsync(content);
    }
}
