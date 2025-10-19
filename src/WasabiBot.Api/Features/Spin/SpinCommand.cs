using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using OpenTelemetry.Trace;
using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.Api.Features.Spin;

internal class SpinCommand
{
    public const string Name = "spin";
    public const string Description = "Spin a wheel with 2-7 options and pick one at random.";

    internal static string ChooseOption(IReadOnlyList<string> options, Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Count, 7, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Count, 2, nameof(options));

        var rng = random ?? Random.Shared;
        var index = rng.Next(options.Count);
        return options[index];
    }

    public static async Task ExecuteAsync(Tracer tracer, ILogger<SpinCommand> logger, ApplicationCommandContext ctx,
        [SlashCommandParameter(Name = "option1", Description = "First option")] string option1,
        [SlashCommandParameter(Name = "option2", Description = "Second option")] string option2,
        [SlashCommandParameter(Name = "option3", Description = "Third option")] string? option3 = null,
        [SlashCommandParameter(Name = "option4", Description = "Fourth option")] string? option4 = null,
        [SlashCommandParameter(Name = "option5", Description = "Fifth option")] string? option5 = null,
        [SlashCommandParameter(Name = "option6", Description = "Sixth option")] string? option6 = null,
        [SlashCommandParameter(Name = "option7", Description = "Seventh option")] string? option7 = null)
    {
        using var span = tracer.StartActiveSpan("spin.choose");

        var userDisplayName = ctx.Interaction.User.GlobalName ?? ctx.Interaction.User.Username;
        logger.LogInformation("Spin command invoked by user {User} in channel {ChannelId}", userDisplayName,
            ctx.Interaction.Channel.Id);

        var options = new[] { option1, option2, option3, option4, option5, option6, option7 }
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        switch (options.Count)
        {
            case < 2:
                logger.LogWarning("Spin command received insufficient options ({Count}) from user {User}",
                    options.Count, userDisplayName);
                await ctx.Interaction.SendResponseAsync(InteractionCallback.Message(
                    InteractionUtils.CreateMessage("Please provide at least 3 distinct options.", ephemeral: true)));
                return;
            case > 7:
                logger.LogWarning("Spin command received too many options ({Count}) from user {User}", options.Count,
                    userDisplayName);
                await ctx.Interaction.SendResponseAsync(InteractionCallback.Message(
                    InteractionUtils.CreateMessage("Please limit to at most 7 options.", ephemeral: true)));
                return;
        }

        var chosen = ChooseOption(options);
        var displayOptions = string.Join(", ", options);
        var response = $"Spinning the wheel! Options: {displayOptions}\nThe wheel lands on... **{chosen}**";

        logger.LogInformation("Spin command selected '{Chosen}' for user {User}", chosen, userDisplayName);
        await ctx.Interaction.SendResponseAsync(InteractionCallback.Message(InteractionUtils.CreateMessage(response)));
    }
}
