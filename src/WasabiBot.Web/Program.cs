using System.Data;
using Amazon.Runtime;
using Amazon.Util;
using MassTransit;
using Npgsql;
using OpenTelemetry.Trace;
using WasabiBot.Web;
using WasabiBot.Web.Commands;
using WasabiBot.Core.Interfaces;
using WasabiBot.DataAccess.Consumers;
using WasabiBot.DataAccess.MassTransit;
using WasabiBot.DataAccess.Messages;
using WasabiBot.DataAccess.Services;
using WasabiBot.Web.Endpoints;
using WasabiBot.Web.Services;
using WasabiBot.Web.Settings;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration.AddEnvironmentVariables();
builder.Services.Configure<DiscordSettings>(builder.Configuration.GetSection("Discord"));

// Add service defaults & Aspire components.
builder.AddServiceDefaults();
builder.Services.AddSingleton(TracerProvider.Default.GetTracer("wasabi_bot"));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, WebJsonContext.Default);
});

var connectionString = builder.Configuration.GetConnectionString("wasabiBotDb"); // TODO: fixme
builder.Services.AddTransient<IDbConnection>(_ => new NpgsqlConnection(connectionString));
builder.Services.AddScoped<InteractionRecordService>();
builder.Services.AddScoped<IInteractionService, InteractionService>();
builder.Services.AddScoped<IDiscordService, DiscordService>();
builder.Services.AddCommandHandlers();
builder.Services.AddHttpClient();
builder.Services.AddMassTransit(x => // todo: move this to a separate method
{
    x.AddConsumer(typeof(InteractionDeferredConsumer));
    x.AddConsumer(typeof(InteractionReceivedConsumer));

    if (builder.Environment.IsDevelopment())
    {
        x.UsingInMemory((ctx, cfg) =>
        {
            cfg.ConfigureEndpoints(ctx);
        });
    }
    else
    {
        x.UsingAmazonSqs((ctx, cfg) =>
        {
            var masstransitConfig = builder.Configuration.GetSection("MassTransit");
            // Use "-error" suffix for error queues.
            cfg.SendTopology.ErrorQueueNameFormatter = new CustomErrorQueueNameFormatter();
            
            // No host config is required, the AWS SDK should pick up the credentials from the environment.
            cfg.Host("us-east-1", host =>
            {
                var fileName = Environment.GetEnvironmentVariable("AWS_WEB_IDENTITY_TOKEN_FILE");
                var role = Environment.GetEnvironmentVariable("AWS_ROLE_ARN");
                var session = Environment.GetEnvironmentVariable("AWS_ROLE_SESSION_NAME");
                host.Credentials(new AssumeRoleWithWebIdentityCredentials(fileName, role, session));
            });
            
            // Configure the InteractionDeferredConsumer
            var deferredQueueName = masstransitConfig["InteractionDeferredQueueName"]!;
            cfg.ReceiveEndpoint(deferredQueueName, e =>
            {
                e.ConfigureConsumer<InteractionDeferredConsumer>(ctx);
                e.UseMessageRetry(r => 
                {
                    r.Immediate(3);
                });
                
                e.ConfigureConsumeTopology = false;
                e.Subscribe(deferredQueueName);
            });
            cfg.Message<InteractionDeferredMessage>(m =>
            {
                m.SetEntityName(deferredQueueName);
            });
            
            // Configure the InteractionReceivedConsumer
            var receivedQueueName = masstransitConfig["InteractionReceivedQueueName"]!;
            cfg.ReceiveEndpoint(receivedQueueName, e =>
            {
                e.ConfigureConsumer<InteractionReceivedConsumer>(ctx);
                
                e.UseMessageRetry(r => 
                {
                    r.Exponential(3, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(5));
                });
                
                e.ConfigureConsumeTopology = false;
                e.Subscribe(receivedQueueName);
            });
            cfg.Message<InteractionReceivedMessage>(m =>
            {
                m.SetEntityName(receivedQueueName);
            });
        });
    }
});

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/", () => "Hello, world!");

var v1 = app.MapGroup("/v1");
v1.MapEndpoints();

Console.WriteLine("Starting the application...");

app.Run();