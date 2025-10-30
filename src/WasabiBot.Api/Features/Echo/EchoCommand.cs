using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Echo;

[CommandHandler("echo", "Echoes back your message", nameof(ExecuteAsync))]
public class EchoCommand
{
    private readonly ILogger<EchoCommand> _logger;

    public EchoCommand(ILogger<EchoCommand> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync(ICommandContext ctx, string message)
    {
        _logger.LogInformation("Echo command invoked with message: {Message}", message);
        await ctx.RespondAsync(message);
    }
}
