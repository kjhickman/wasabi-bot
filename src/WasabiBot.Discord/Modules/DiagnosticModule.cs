using Discord.Interactions;

namespace WasabiBot.Discord.Modules;

public class DiagnosticModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("ping", "Pings the bot and returns its latency.")]
    public async Task GreetUserAsync()
        => await RespondAsync(text: $":ping_pong: It took me {Context.Client.Latency}ms to respond to you!", ephemeral: true);
}