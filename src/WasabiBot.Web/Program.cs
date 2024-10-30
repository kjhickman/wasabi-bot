using System.Data;
using Amazon.SQS;
using Npgsql;
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

builder.Configuration.AddEnvironmentVariables();
builder.Services.Configure<EnvironmentVariables>(builder.Configuration);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

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
// builder.Services.AddScoped<IMessageHandler<DeferredInteractionMessage>, InteractionMessageHandler>();
// builder.Services.AddScoped<IMessageHandler<InteractionReceivedMessage>, InteractionReceivedHandler>();
builder.Services.AddSingleton<IAmazonSQS, AmazonSQSClient>();
builder.Services.AddHttpClient();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/", () => "Hello, world!");

var v1 = app.MapGroup("/v1");
v1.MapEndpoints();

app.Run();
