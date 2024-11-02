namespace WasabiBot.Core.Discord.Enums;

/// <summary>
/// Represents the type of response that can be sent for a Discord interaction.
/// See the <a href="https://discord.com/developers/docs/interactions/application-commands#application-command-object-application-command-types">Discord API documentation</a> for more details.
/// </summary>
public enum InteractionResponseType
{
    /// <summary>
    /// Acknowledges a Ping interaction with a Pong response.
    /// Used for the initial webhook ping.
    /// </summary>
    Pong = 1,

    /// <summary>
    /// Responds to an interaction with a message, showing the user's input.
    /// This creates a new message as a response to the interaction.
    /// </summary>
    ChannelMessageWithSource = 4,

    /// <summary>
    /// Acknowledges an interaction and shows a loading state to the user.
    /// Follow up with an edit to respond with a message later.
    /// </summary>
    DeferredChannelMessageWithSource = 5,

    /// <summary>
    /// Acknowledges a component interaction and shows a loading state.
    /// Follow up with an edit to update the original message later.
    /// </summary>
    DeferredUpdateMessage = 6,

    /// <summary>
    /// Updates the message that the component was attached to.
    /// Used for updating the original message in response to a component interaction.
    /// </summary>
    UpdateMessage = 7,

    /// <summary>
    /// Responds to an autocomplete interaction with suggested choices.
    /// Used to provide autocomplete results for application command options.
    /// </summary>
    ApplicationCommandAutocompleteResult = 8,

    /// <summary>
    /// Responds to an interaction with a popup modal.
    /// Used to show a modal dialog in response to a user interaction.
    /// </summary>
    Modal = 9
}
