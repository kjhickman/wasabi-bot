using dotenv.net;
using VinceBot;
using VinceBot.CommandHandlers;
using VinceBot.Endpoints;
using VinceBot.Interfaces;
using VinceBot.Services;
using VinceBot.Settings;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, JsonContext.Default);
});
builder.Services.AddScoped<IInteractionService, InteractionService>();
builder.Services.AddScoped<ICommandsService, CommandsService>();
builder.Services.AddHttpClient();
builder.Services.AddCommandHandlers();

DotEnv.Load();
builder.Configuration.AddEnvironmentVariables();
builder.Services.Configure<DiscordSettings>(builder.Configuration.GetSection("Discord"));

var app = builder.Build();

app.MapGet("/", () => "Hello, world!");
app.MapDiscordEndpoints();

app.Run();