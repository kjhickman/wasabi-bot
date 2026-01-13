using Microsoft.Extensions.AI;

namespace WasabiBot.Api.Infrastructure.AI;

/// <summary>
/// Factory for creating chat clients based on named presets at runtime.
/// </summary>
public interface IChatClientFactory
{
    /// <summary>
    /// Gets a chat client for the specified preset.
    /// </summary>
    IChatClient GetChatClient(LlmPreset preset = LlmPreset.LowLatency);
}
