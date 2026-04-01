using NetCord.Rest;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Mock;

[MessageCommandHandler("Mock")]
internal sealed class MockCommand
{
    private readonly ILogger<MockCommand> _logger;

    public MockCommand(ILogger<MockCommand> logger)
    {
        _logger = logger;
    }

    public Task<InteractionMessageProperties> ExecuteAsync(RestMessage message)
    {
        try
        {
            _logger.LogInformation(
                "Mock command invoked for message {MessageId} in channel {ChannelId} by author {Author}",
                message.Id,
                message.ChannelId,
                message.Author.Username);

            if (string.IsNullOrWhiteSpace(message.Content))
            {
                return Task.FromResult(InteractionUtils.CreateMessage("That message has no text to mock.", ephemeral: true));
            }

            var mockedMessage = Mockify(message.Content);

            _logger.LogInformation(
                "Mock command generated mocked text for message {MessageId}",
                message.Id);

            return Task.FromResult(InteractionUtils.CreateMessage(mockedMessage));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mock command failed for message {MessageId}", message.Id);
            return Task.FromResult(InteractionUtils.CreateMessage("Something went wrong while mocking that message. Please try again later.", ephemeral: true));
        }
    }

    internal static string Mockify(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var chars = message.ToCharArray();
        var uppercase = true;

        for (var i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetter(chars[i]))
            {
                continue;
            }

            chars[i] = uppercase
                ? char.ToUpperInvariant(chars[i])
                : char.ToLowerInvariant(chars[i]);

            uppercase = !uppercase;
        }

        return new string(chars);
    }
}
