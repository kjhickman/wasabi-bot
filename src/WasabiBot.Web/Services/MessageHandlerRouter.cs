using System.Text.Json;
using FluentResults;
using WasabiBot.Core.Interfaces;
using WasabiBot.DataAccess;
using WasabiBot.DataAccess.Handlers;
using WasabiBot.DataAccess.Messages;

namespace WasabiBot.Web.Services;

public class MessageHandlerRouter
{
    private readonly IServiceProvider _serviceProvider;

    public MessageHandlerRouter(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<Result> Route(string messageBody, string handlerName, CancellationToken ct = default)
    {
        switch (handlerName)
        {
            case nameof(InteractionMessageHandler):
                var deferredInteractionHandler = _serviceProvider.GetRequiredService<IMessageHandler<DeferredInteractionMessage>>();
                return await InvokeHandler(deferredInteractionHandler, messageBody, ct);
            case nameof(InteractionReceivedHandler):
                var interactionReceivedHandler = _serviceProvider.GetRequiredService<IMessageHandler<InteractionReceivedMessage>>();
                return await InvokeHandler(interactionReceivedHandler, messageBody, ct);
            default:
                return Result.Fail($"Unknown message handler name: {handlerName}");
        }
    }

    private static async Task<Result> InvokeHandler<T>(IMessageHandler<T> handler, string messageBody, CancellationToken ct = default) where T : IMessage
    {
        var messageJsonTypeInfo = DataAccessJsonContext.Default.GetTypeInfo(handler.MessageType);
        if (messageJsonTypeInfo is null)
        {
            return Result.Fail($"Unable to get message json type info from {handler.MessageType.Name}");
        }

        if (JsonSerializer.Deserialize(messageBody, messageJsonTypeInfo) is not IMessage message)
        {
            return Result.Fail($"Failed to deserialize message body for {handler.MessageType.Name}");
        }
                
        return await handler.Handle(message, ct);
    }
}
