using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace WasabiBot.Api.Services;

/// <summary>
/// Factory for creating standardized <see cref="InteractionResponder"/> instances used by slash command handlers.
/// Centralizes threshold and response wiring so command code stays focused on business logic.
/// </summary>
internal static class InteractionResponderFactory
{
    private static readonly TimeSpan DefaultThreshold = TimeSpan.FromMilliseconds(2300);

    /// <summary>
    /// Creates an <see cref="InteractionResponder"/> bound to the provided command context.
    /// </summary>
    /// <param name="ctx">The application command context.</param>
    /// <param name="threshold">Optional override for defer threshold. Defaults to ~2.3 seconds.</param>
    /// <returns>A new <see cref="InteractionResponder"/> instance (caller is responsible for disposing).</returns>
    public static InteractionResponder Create(ApplicationCommandContext ctx, TimeSpan? threshold = null)
    {
        return new InteractionResponder(
            threshold: threshold ?? DefaultThreshold,
            defer: _ => ctx.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage()),
            respond: (text, ephemeral) => ctx.Interaction.SendResponseAsync(InteractionCallback.Message(InteractionMessageFactory.Create(text, ephemeral))),
            followup: (text, ephemeral) => ctx.Interaction.SendFollowupMessageAsync(InteractionMessageFactory.Create(text, ephemeral)));
    }
}

