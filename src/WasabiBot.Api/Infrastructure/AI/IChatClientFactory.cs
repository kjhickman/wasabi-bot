using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;

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

    /// <summary>
    /// Gets the model name for the specified preset.
    /// </summary>
    string GetModelName(LlmPreset preset = LlmPreset.LowLatency);
}

/// <summary>
/// Default implementation of IChatClientFactory that creates and caches providers based on configuration.
/// </summary>
internal sealed class ChatClientFactory : IChatClientFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<OpenRouterOptions> _options;
    private readonly Dictionary<LlmPreset, IChatClient> _clientCache = [];

    public ChatClientFactory(IServiceProvider serviceProvider, IOptions<OpenRouterOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options;
    }

    public IChatClient GetChatClient(LlmPreset preset = LlmPreset.LowLatency)
    {
        if (_clientCache.TryGetValue(preset, out var cachedClient))
        {
            return cachedClient;
        }

        var chatClient = CreateOpenRouterChatClient(preset);
        _clientCache[preset] = chatClient;
        return chatClient;
    }

    public string GetModelName(LlmPreset preset = LlmPreset.LowLatency)
    {
        return preset switch
        {
            LlmPreset.LowLatency => _options.Value.LowLatency,
            LlmPreset.LowLatencyCreative => _options.Value.LowLatencyCreative,
            _ => _options.Value.LowLatency
        };
    }

    private IChatClient CreateOpenRouterChatClient(LlmPreset preset)
    {
        var options = _options.Value;
        var apiKey = options.ApiKey ?? throw new InvalidOperationException("OpenRouter API key is not configured.");

        var modelName = GetModelName(preset);

        var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(options.Endpoint) };
        var chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions)
            .GetChatClient(modelName)
            .AsIChatClient();

        return new ChatClientBuilder(chatClient)
            .UseOpenTelemetry(_serviceProvider.GetRequiredService<ILoggerFactory>(), "Microsoft.Extensions.AI")
            .UseFunctionInvocation()
            .UseLogging()
            .Build(_serviceProvider);
    }
}
