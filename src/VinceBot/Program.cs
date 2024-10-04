using dotenv.net;
using VinceBot;
using VinceBot.Endpoints;
using VinceBot.Services;

var builder = WebApplication.CreateSlimBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, JsonContext.Default);
});
builder.Services.AddScoped<IInteractionService, InteractionService>();

DotEnv.Load();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.AddDiscordEndpoints();

app.Run();