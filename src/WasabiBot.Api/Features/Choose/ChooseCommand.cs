using OpenTelemetry.Trace;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Choose;

[CommandHandler("choose", "Choose randomly from 2-7 options.")]
internal sealed class ChooseCommand
{
    private readonly Tracer _tracer;
    private readonly ILogger<ChooseCommand> _logger;

    public ChooseCommand(Tracer tracer, ILogger<ChooseCommand> logger)
    {
        _tracer = tracer;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        ICommandContext ctx,
        string option1,
        string option2,
        string? option3 = null,
        string? option4 = null,
        string? option5 = null,
        string? option6 = null,
        string? option7 = null)
    {
        try
        {
            using var span = _tracer.StartActiveSpan("choose.execute");

            var userDisplayName = ctx.UserDisplayName;
            var channelId = ctx.ChannelId;

            _logger.LogInformation(
                "Choose command invoked by user {User} in channel {ChannelId}",
                userDisplayName,
                channelId);

            var options = new[] { option1, option2, option3, option4, option5, option6, option7 }
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Select(o => o!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            switch (options.Count)
            {
                case < 2:
                    _logger.LogWarning(
                        "Choose command received insufficient options ({Count}) from user {User}",
                        options.Count,
                        userDisplayName);
                    await ctx.SendEphemeralAsync("Please provide at least 3 distinct options.");
                    return;
                case > 7:
                    _logger.LogWarning(
                        "Choose command received too many options ({Count}) from user {User}",
                        options.Count,
                        userDisplayName);
                    await ctx.SendEphemeralAsync("Please limit to at most 7 options.");
                    return;
            }

            var chosen = ChooseOption(options);
            var displayOptions = string.Join(", ", options);
            var response = $"Options: {displayOptions}\nAnd the choice is... **{chosen}**";

            _logger.LogInformation(
                "Choose command selected '{Chosen}' for user {User}",
                chosen,
                userDisplayName);
            await ctx.RespondAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Choose command failed for user {User}", ctx.UserDisplayName);
            await ctx.SendEphemeralAsync("Something went wrong while processing that command. Please try again later.");
        }
    }

    internal static string ChooseOption(IReadOnlyList<string> options, Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Count, 7, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Count, 2, nameof(options));

        var rng = random ?? Random.Shared;
        var index = rng.Next(options.Count);
        return options[index];
    }
}
