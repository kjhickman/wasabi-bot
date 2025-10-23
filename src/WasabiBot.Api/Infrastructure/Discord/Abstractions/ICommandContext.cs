namespace WasabiBot.Api.Infrastructure.Discord.Abstractions;

public interface ICommandContext
{
    Task RespondAsync(string message, bool ephemeral = false);
    Task SendEphemeralAsync(string content);
    ulong ChannelId { get; }
    ulong UserId { get; }
    string UserDisplayName { get; }
}
