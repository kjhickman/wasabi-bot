using System.Data;
using Amazon.SQS;
using dotenv.net;
using Npgsql;
using Serilog;
using Serilog.Formatting.Compact;
using WasabiBot;
using WasabiBot.Commands;
using WasabiBot.Endpoints;
using WasabiBot.Interfaces;
using WasabiBot.Messaging.Handlers;
using WasabiBot.Messaging.Messages;
using WasabiBot.Services;
using WasabiBot.Settings;

var builder = WebApplication.CreateSlimBuilder(args);

DotEnv.Load(); // todo: only run if development
builder.Configuration.AddEnvironmentVariables();
builder.Services.Configure<EnvironmentVariables>(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, JsonContext.Default);
});
builder.Services.AddScoped<IInteractionService, InteractionService>();
builder.Services.AddScoped<IDiscordService, DiscordService>();
builder.Services.AddHttpClient();
builder.Services.AddCommandHandlers();
builder.Services.AddSingleton<IAmazonSQS, AmazonSQSClient>();
builder.Services.AddTransient<IDbConnection>(_ => new NpgsqlConnection(builder.Configuration.GetConnectionString("Postgres")));
builder.Services.AddScoped<InteractionRecordService>();
builder.Services.AddScoped<IMessageHandler<DeferredInteractionMessage>, InteractionMessageHandler>();
builder.Services.AddScoped<IMessageHandler<InteractionReceivedMessage>, InteractionReceivedHandler>();
builder.Services.AddScoped<MessageHandlerRouter>();
builder.Services.AddScoped<IMessageClient, SqsMessageClient>();

builder.Logging.ClearProviders();
ILogger logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateLogger();
Log.Logger = logger;
builder.Services.AddSerilog(logger);

var app = builder.Build();

app.MapGet("/warmup", () => TypedResults.Ok());
var v1 = app.MapGroup("/v1");
v1.MapEndpoints();

app.Run();
