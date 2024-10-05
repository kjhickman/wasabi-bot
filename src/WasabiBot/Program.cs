using Amazon.SQS;
using dotenv.net;
using WasabiBot;
using WasabiBot.Commands;
using WasabiBot.Endpoints;
using WasabiBot.Interfaces;
using WasabiBot.Services;
using WasabiBot.Settings;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Logging.AddJsonConsole();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, JsonContext.Default);
});
builder.Services.AddScoped<IInteractionService, InteractionService>();
builder.Services.AddScoped<IDiscordService, DiscordService>();
builder.Services.AddHttpClient();
builder.Services.AddCommandHandlers();
builder.Services.AddSingleton<IAmazonSQS, AmazonSQSClient>();

DotEnv.Load();
builder.Configuration.AddEnvironmentVariables();
builder.Services.Configure<EnvironmentVariables>(builder.Configuration);

var app = builder.Build();

app.MapGet("/", () => "Hello, world!");
app.MapDiscordEndpoints();
app.MapEventEndpoints();

app.Run();
