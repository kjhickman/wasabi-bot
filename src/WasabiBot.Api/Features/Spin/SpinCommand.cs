using OpenTelemetry.Trace;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Spin;

[CommandHandler("spin", "Spin a wheel with 2-7 options and pick one at random.", nameof(ExecuteAsync))]
internal sealed class SpinCommand
{
    private readonly Tracer _tracer;
    private readonly ILogger<SpinCommand> _logger;

    public SpinCommand(Tracer tracer, ILogger<SpinCommand> logger)
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
        using var span = _tracer.StartActiveSpan("spin.choose");

        var userDisplayName = ctx.UserDisplayName;
        var channelId = ctx.ChannelId;

        _logger.LogInformation(
            "Spin command invoked by user {User} in channel {ChannelId}",
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
                    "Spin command received insufficient options ({Count}) from user {User}",
                    options.Count,
                    userDisplayName);
                await ctx.SendEphemeralAsync("Please provide at least 3 distinct options.");
                return;
            case > 7:
                _logger.LogWarning(
                    "Spin command received too many options ({Count}) from user {User}",
                    options.Count,
                    userDisplayName);
                await ctx.SendEphemeralAsync("Please limit to at most 7 options.");
                return;
        }

        var chosen = ChooseOption(options);
        var displayOptions = string.Join(", ", options);
        var response = $"Spinning the wheel! Options: {displayOptions}\nThe wheel lands on... **{chosen}**";

        _logger.LogInformation(
            "Spin command selected '{Chosen}' for user {User}",
            chosen,
            userDisplayName);
        await ctx.RespondAsync(response);
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
