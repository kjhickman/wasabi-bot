using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace WasabiBot.Api.Infrastructure.AI;

/// <summary>
/// Default implementation of IChatClientFactory that creates and caches providers based on configuration.
/// </summary>
internal sealed class ChatClientFactory : IChatClientFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<OpenRouterV2Options> _options;
    private readonly Dictionary<LlmPreset, IChatClient> _clientCache = [];

    public ChatClientFactory(IServiceProvider serviceProvider, IOptionsMonitor<OpenRouterV2Options> options)
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

    private IChatClient CreateOpenRouterChatClient(LlmPreset preset)
    {
        var options = _options.CurrentValue;
        var apiKey = options.ApiKey ?? throw new InvalidOperationException("OpenRouter API key is not configured.");

        var modelName = GetModelName(preset);

        var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(AIConstants.Endpoints.OpenRouter) };
        var chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions)
            .GetChatClient(modelName)
            .AsIChatClient();

        return new ChatClientBuilder(chatClient)
            .UseOpenTelemetry(_serviceProvider.GetRequiredService<ILoggerFactory>(), "Microsoft.Extensions.AI")
            .UseFunctionInvocation()
            .UseLogging()
            .Build(_serviceProvider);
    }

    private string GetModelName(LlmPreset preset = LlmPreset.LowLatency)
    {
        return preset switch
        {
            LlmPreset.LowLatency => _options.CurrentValue.LowLatencyPreset,
            LlmPreset.LowLatencyCreative => _options.CurrentValue.LowLatencyCreative,
            _ => _options.CurrentValue.LowLatencyPreset
        };
    }
}