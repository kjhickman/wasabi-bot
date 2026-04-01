using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Flip;

[CommandHandler("flip", "Flip a coin.")]
internal sealed class FlipCommand(ILogger<FlipCommand> logger)
{
    private readonly ILogger<FlipCommand> _logger = logger;

    public async Task ExecuteAsync(ICommandContext ctx)
    {
        try
        {
            _logger.LogInformation(
                "Flip command invoked by user {User} ({UserId}) in channel {ChannelId}",
                ctx.UserDisplayName,
                ctx.UserId,
                ctx.ChannelId);

            var result = FlipCoin();

            _logger.LogInformation(
                "Flip command landed on {Result} for user {User}",
                result,
                ctx.UserDisplayName);

            await ctx.RespondAsync($"The coin lands on... **{result}!**");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Flip command failed for user {User}", ctx.UserDisplayName);
            await ctx.SendEphemeralAsync("Something went wrong while flipping the coin. Please try again later.");
        }
    }

    internal static string FlipCoin(Random? random = null)
    {
        var rng = random ?? Random.Shared;
        return rng.Next(2) == 0 ? "Heads" : "Tails";
    }
}
