global using ILogger = Serilog.ILogger;
using System.Data;
using Amazon.SQS;
using dotenv.net;
using Npgsql;
using Serilog;
using Serilog.Formatting.Compact;
using WasabiBot.Web;
using WasabiBot.Web.Commands;
using WasabiBot.Core.Interfaces;
using WasabiBot.DataAccess.Handlers;
using WasabiBot.DataAccess.Messages;
using WasabiBot.DataAccess.Services;
using WasabiBot.DataAccess.Settings;
using WasabiBot.Web.Endpoints;
using WasabiBot.Web.Services;

var builder = WebApplication.CreateSlimBuilder(args);

DotEnv.Load(); // todo: only run if development
builder.Configuration.AddEnvironmentVariables();
builder.Services.Configure<EnvironmentVariables>(builder.Configuration);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, WebJsonContext.Default);
});
builder.Services.AddScoped<IInteractionService, InteractionService>();
builder.Services.AddScoped<IDiscordService, DiscordService>();
builder.Services.AddHttpClient();
builder.Services.AddCommandHandlers();
builder.Services.AddSingleton<IAmazonSQS, AmazonSQSClient>();
builder.Services.AddTransient<IDbConnection>(_ => new NpgsqlConnection(builder.Configuration.GetConnectionString("wasabiBotDb")));
builder.Services.AddScoped<InteractionRecordService>();
builder.Services.AddScoped<IMessageHandler<DeferredInteractionMessage>, InteractionMessageHandler>();
builder.Services.AddScoped<IMessageHandler<InteractionReceivedMessage>, InteractionReceivedHandler>();
builder.Services.AddScoped<MessageHandlerRouter>();

builder.Logging.ClearProviders();
ILogger logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateLogger();
Log.Logger = logger;
builder.Services.AddSingleton(logger);

var app = builder.Build();

var v1 = app.MapGroup("/v1");
v1.MapEndpoints();

app.Run();
