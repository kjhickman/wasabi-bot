using Microsoft.Extensions.AI;

namespace WasabiBot.UnitTests.Builders;

public sealed class ChatResponseBuilder
{
    private readonly List<ChatMessage> _messages = [];
    private string? _responseId;
    private string? _conversationId;
    private string? _modelId;
    private DateTimeOffset? _createdAt;
    private ChatFinishReason? _finishReason;
    private UsageDetails? _usage;
    private object? _rawRepresentation;
    private AdditionalPropertiesDictionary? _additionalProperties;

    public static ChatResponseBuilder Create() => new();

    public ChatResponseBuilder WithMessage(ChatMessage message)
    {
        _messages.Add(message);
        return this;
    }

    public ChatResponseBuilder WithMessages(IEnumerable<ChatMessage> messages)
    {
        _messages.AddRange(messages);
        return this;
    }

    public ChatResponseBuilder WithAssistantText(string text)
    {
        var message = new ChatMessage(ChatRole.Assistant, [new TextContent(text)]);
        return WithMessage(message);
    }

    public ChatResponseBuilder WithUserText(string text)
    {
        var message = new ChatMessage(ChatRole.User, [new TextContent(text)]);
        return WithMessage(message);
    }

    public ChatResponseBuilder WithToolResult(string callId, object result)
    {
        var message = new ChatMessage(ChatRole.Tool, [new FunctionResultContent(callId, result)]);
        return WithMessage(message);
    }

    public ChatResponseBuilder WithResponseId(string responseId)
    {
        _responseId = responseId;
        return this;
    }

    public ChatResponseBuilder WithConversationId(string conversationId)
    {
        _conversationId = conversationId;
        return this;
    }

    public ChatResponseBuilder WithModelId(string modelId)
    {
        _modelId = modelId;
        return this;
    }

    public ChatResponseBuilder WithCreatedAt(DateTimeOffset createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public ChatResponseBuilder WithFinishReason(ChatFinishReason finishReason)
    {
        _finishReason = finishReason;
        return this;
    }

    public ChatResponseBuilder WithUsage(Action<UsageDetails> configure)
    {
        _usage ??= new UsageDetails();
        configure(_usage);
        return this;
    }

    public ChatResponseBuilder WithRawRepresentation(object rawRepresentation)
    {
        _rawRepresentation = rawRepresentation;
        return this;
    }

    public ChatResponseBuilder WithAdditionalProperties(AdditionalPropertiesDictionary properties)
    {
        _additionalProperties = properties;
        return this;
    }

    public ChatResponse Build()
    {
        var messages = _messages.ToList();

        var response = messages.Count switch
        {
            0 => new ChatResponse(),
            1 => new ChatResponse(messages[0]),
            _ => new ChatResponse(messages)
        };

        response.Messages = messages;

        if (_responseId is not null)
            response.ResponseId = _responseId;
        if (_conversationId is not null)
            response.ConversationId = _conversationId;
        if (_modelId is not null)
            response.ModelId = _modelId;
        if (_createdAt is not null)
            response.CreatedAt = _createdAt;
        if (_finishReason is not null)
            response.FinishReason = _finishReason;
        if (_usage is not null)
            response.Usage = _usage;
        if (_rawRepresentation is not null)
            response.RawRepresentation = _rawRepresentation;
        if (_additionalProperties is not null)
            response.AdditionalProperties = _additionalProperties;

        return response;
    }
}
