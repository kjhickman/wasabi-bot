using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace WasabiBot.Api.Infrastructure.Discord.Interactions;

/// <summary>
/// Provides an "auto-defer" wrapper for Discord interactions so commands can
/// respond immediately when fast, but safely defer and follow up when work approaches
/// the platform's acknowledgement deadline (typically ~3 seconds).
/// </summary>
/// <remarks>
/// Usage pattern:
/// <code>
/// await using var responder = new AutoResponder(
///     threshold: TimeSpan.FromMilliseconds(2300),
///     defer: ephemeral => interaction.SendResponseAsync(InteractionCallback.DeferredMessage()),
///     respond: (text, ephemeral) => interaction.SendResponseAsync(InteractionCallback.Message(text)),
///     followup: (text, ephemeral) => interaction.SendFollowupMessageAsync(text));
///
/// // Do work...
/// await responder.SendAsync(resultText);
/// </code>
/// If the work completes before <c>threshold</c>, <see cref="SendAsync(string,bool)"/> sends the initial response.
/// Otherwise, the responder auto-defers first and <see cref="SendAsync(string,bool)"/> posts a follow-up message.
/// Thread-safe acknowledgement is enforced via <see cref="Interlocked"/> so the interaction is only
/// acknowledged once.
/// </remarks>
internal sealed class InteractionResponder : IAsyncDisposable
{
    private static readonly TimeSpan DefaultThreshold = TimeSpan.FromMilliseconds(2300);
    private readonly Func<string, bool, Task> _respond;
    private readonly Func<string, bool, Task> _followup;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _timerTask;

    // 0 = none, 1 = deferred, 2 = responded
    private int _state;

    /// <summary>
    /// Creates a new auto-responder that will defer the interaction if the initial
    /// response has not been sent within the specified <paramref name="threshold"/>.
    /// </summary>
    /// <param name="threshold">The time to wait before sending a deferred acknowledgement.</param>
    /// <param name="defer">Callback that sends a deferred response for the interaction. Receives <c>ephemeral</c>.</param>
    /// <param name="respond">Callback that sends the initial response. Receives content and <c>ephemeral</c>.</param>
    /// <param name="followup">Callback that sends a follow-up message. Receives content and <c>ephemeral</c>.</param>
    internal InteractionResponder(
        TimeSpan threshold,
        Func<bool, Task> defer,
        Func<string, bool, Task> respond,
        Func<string, bool, Task> followup)
    {
        _respond = respond;
        _followup = followup;

        _timerTask = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(threshold, _cts.Token);
                if (Interlocked.CompareExchange(ref _state, 1, 0) == 0)
                {
                    // Auto-defer typically does not need to be ephemeral; pass false by default
                    await defer(false);
                }
            }
            catch (TaskCanceledException)
            {
                // ignored
            }
        });
    }

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
            respond: (text, ephemeral) => ctx.Interaction.SendResponseAsync(InteractionCallback.Message(InteractionUtils.CreateMessage(text, ephemeral))),
            followup: (text, ephemeral) => ctx.Interaction.SendFollowupMessageAsync(InteractionUtils.CreateMessage(text, ephemeral)));
    }

    /// <summary>
    /// Sends the response content using the correct channel based on acknowledgement state.
    /// </summary>
    /// <param name="content">The text content to send.</param>
    /// <param name="ephemeral">When true, attempts to send the message as ephemeral.</param>
    /// <remarks>
    /// - If called before the threshold elapses and no one has acknowledged the interaction,
    ///   this sends the initial response.
    /// - If the threshold elapsed (auto-defer ran) or an initial response was already sent,
    ///   this sends a follow-up message.
    /// - Multiple calls after the first acknowledgement always result in follow-ups.
    /// </remarks>
    public async Task SendAsync(string content, bool ephemeral = false)
    {
        // Cancel pending auto-defer if any
        await _cts.CancelAsync();

        // Try to be the first to acknowledge
        var previous = Interlocked.CompareExchange(ref _state, 2, 0);
        if (previous == 0)
        {
            // We won the race: send initial response
            await _respond(content, ephemeral);
            return;
        }

        // Already deferred or responded: send followup
        await _followup(content, ephemeral);
    }

    public async Task SendEphemeralAsync(string content)
    {
        await SendAsync(content, ephemeral: true);
    }

    /// <summary>
    /// Cancels the pending auto-defer (if any) and waits for the internal timer task to complete.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        try
        {
            await _timerTask.ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }
        _cts.Dispose();
    }
}
