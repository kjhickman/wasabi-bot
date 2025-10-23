using WasabiBot.Api.Infrastructure.Discord.Abstractions;

namespace WasabiBot.UnitTests.Infrastructure.Discord;

internal sealed class FakeCommandContext : ICommandContext
{
    private readonly List<(string Message, bool Ephemeral)> _messages = [];

    public FakeCommandContext(
        ulong userId = 1,
        ulong channelId = 2,
        string userDisplayName = "TestUser")
    {
        UserId = userId;
        ChannelId = channelId;
        UserDisplayName = userDisplayName;
    }

    public IReadOnlyList<(string Message, bool Ephemeral)> Messages => _messages;

    public IReadOnlyList<string> EphemeralMessages => _messages
        .Where(static m => m.Ephemeral)
        .Select(static m => m.Message)
        .ToList();

    public ulong ChannelId { get; }

    public ulong UserId { get; }
    public string UserDisplayName { get; }


    public Task RespondAsync(string message, bool ephemeral = false)
    {
        _messages.Add((message, ephemeral));
        return Task.CompletedTask;
    }

    public Task SendEphemeralAsync(string content) => RespondAsync(content, ephemeral: true);
}
