using System.Data;
using Amazon.SQS;
using MassTransit;
using Npgsql;
using OpenTelemetry.Trace;
using WasabiBot.Web;
using WasabiBot.Web.Commands;
using WasabiBot.Core.Interfaces;
using WasabiBot.DataAccess.Consumers;
using WasabiBot.DataAccess.Messages;
using WasabiBot.DataAccess.Services;
using WasabiBot.DataAccess.Settings;
using WasabiBot.Web.Endpoints;
using WasabiBot.Web.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration.AddEnvironmentVariables();
builder.Services.Configure<EnvironmentVariables>(builder.Configuration);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();
builder.Services.AddSingleton(TracerProvider.Default.GetTracer("wasabi_bot"));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, WebJsonContext.Default);
});

var connectionString = builder.Configuration.GetConnectionString("wasabiBotDb");
builder.Services.AddTransient<IDbConnection>(_ => new NpgsqlConnection(connectionString));
builder.Services.AddScoped<InteractionRecordService>();
builder.Services.AddScoped<IInteractionService, InteractionService>();
builder.Services.AddScoped<IDiscordService, DiscordService>();
builder.Services.AddCommandHandlers();
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer(typeof(InteractionDeferredConsumer));
    x.AddConsumer(typeof(InteractionReceivedConsumer));

    Console.WriteLine(builder.Environment.IsDevelopment());
    x.SetKebabCaseEndpointNameFormatter();
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
            cfg.Host("us-east-1", _ => {});
            cfg.ConfigureEndpoints(ctx);
        });
    }
});
builder.Services.AddHttpClient();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/", () => "Hello, world!");

var v1 = app.MapGroup("/v1");
v1.MapEndpoints();

app.Run();
