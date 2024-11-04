using MassTransit;
using WasabiBot.DataAccess.Consumers;
using WasabiBot.DataAccess.MassTransit;
using WasabiBot.DataAccess.Messages;

namespace WasabiBot.Web.DependencyInjection;

public static class MassTransit
{
    public static void AddMassTransit(this WebApplicationBuilder builder)
    {
        builder.Services.AddMassTransit(x =>
        {
            x.AddConsumer(typeof(InteractionDeferredConsumer));
            x.AddConsumer(typeof(InteractionReceivedConsumer));

            if (builder.Environment.IsDevelopment())
            {
                x.UsingInMemory((ctx, cfg) => { cfg.ConfigureEndpoints(ctx); });
            }
            else
            {
                x.UsingAmazonSqs((ctx, cfg) =>
                {
                    var masstransitConfig = builder.Configuration.GetSection("MassTransit");
                    
                    // Sets the wait time for the bus endpoint
                    cfg.WaitTimeSeconds = 20;
                    
                    // Use "-error" suffix for error queues.
                    cfg.SendTopology.ErrorQueueNameFormatter = new CustomErrorQueueNameFormatter();

                    // No host config is required, the AWS SDK should pick up the credentials from the environment.
                    cfg.Host("us-east-1", _ => { });

                    // Configure the InteractionDeferredConsumer
                    var deferredQueueName = masstransitConfig["InteractionDeferredQueueName"]!;
                    cfg.ReceiveEndpoint(deferredQueueName, e =>
                    {
                        e.ConfigureConsumeTopology = false;
                        e.WaitTimeSeconds = 10;
                        e.Subscribe(deferredQueueName);
                        e.ConfigureConsumer<InteractionDeferredConsumer>(ctx);
                        e.UseMessageRetry(r => r.Immediate(3));
                    });
                    cfg.Message<InteractionDeferredMessage>(m => { m.SetEntityName(deferredQueueName); });

                    // Configure the InteractionReceivedConsumer
                    var receivedQueueName = masstransitConfig["InteractionReceivedQueueName"]!;
                    cfg.ReceiveEndpoint(receivedQueueName, e =>
                    {
                        e.ConfigureConsumeTopology = false;
                        e.Subscribe(receivedQueueName);
                        e.ConfigureConsumer<InteractionReceivedConsumer>(ctx);
                        e.UseMessageRetry(r => r.Immediate(3));
                    });
                    cfg.Message<InteractionReceivedMessage>(m => { m.SetEntityName(receivedQueueName); });
                });
                
                // Polling time for all endpoints
                x.AddConfigureEndpointsCallback((_, cfg) =>
                {
                    if (cfg is IAmazonSqsReceiveEndpointConfigurator sqs)
                        sqs.WaitTimeSeconds = 20;
                });
            }
        });
    }
}