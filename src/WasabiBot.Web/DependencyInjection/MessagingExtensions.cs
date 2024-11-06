using WasabiBot.DataAccess.Handlers;
using WasabiBot.DataAccess.Interfaces;
using WasabiBot.DataAccess.Messages;
using WasabiBot.DataAccess.Services;

namespace WasabiBot.Web.DependencyInjection;

public static class MessagingExtensions
{
    public static void AddMessageHandlers(this WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddInMemoryMessageBus();
            builder.Services.AddInMemorySubscriber<InteractionDeferredHandler, InteractionDeferredMessage>();
            builder.Services.AddInMemorySubscriber<InteractionReceivedConsumer, InteractionReceivedMessage>();
        }
        else
        {
            var messagingConfig = builder.Configuration.GetRequiredSection("Messaging");
            builder.Services.AddSqsMessageBus();
            builder.Services.AddSqsSubscriber<InteractionDeferredHandler, InteractionDeferredMessage>(messagingConfig["InteractionDeferredQueueName"]!);
            builder.Services.AddSqsSubscriber<InteractionReceivedConsumer, InteractionReceivedMessage>(messagingConfig["InteractionReceivedQueueName"]!);
        }
    }
    
    private static void AddSqsMessageBus(this IServiceCollection services)
    {
        services.AddSingleton<IMessageClient, SqsMessageClient>();
    }
    
    private static void AddSqsSubscriber<TSubscriber, TMessage>(this IServiceCollection services, string queueName) 
        where TSubscriber : class, IMessageHandler<TMessage> 
        where TMessage : class
    {
        services.AddScoped<IMessageHandler<TMessage>, TSubscriber>();
        services.AddHostedService<SqsEndpoint<TMessage>>();
        QueueInfo.UrlMap.Add(typeof(TMessage).Name, queueName);
    }
    
    private static void AddInMemoryMessageBus(this IServiceCollection services)
    {
        services.AddSingleton<IMessageClient, InMemoryMessageClient>();
    }

    private static void AddInMemorySubscriber<TSubscriber, TMessage>(this IServiceCollection services)
        where TSubscriber : class, IMessageHandler<TMessage>
        where TMessage : class
    {
        var queueName = typeof(TMessage).Name;
        services.AddScoped<IMessageHandler<TMessage>, TSubscriber>();
        services.AddHostedService<InMemoryEndpoint<TMessage>>();
        QueueInfo.UrlMap.Add(typeof(TMessage).Name, queueName);
        InMemoryMessageBus.EnsureQueueExists(queueName);
    }
}