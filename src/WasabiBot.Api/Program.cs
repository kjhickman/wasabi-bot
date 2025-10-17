using TickerQ.DependencyInjection;
using WasabiBot.Api.Infrastructure.AI;
using WasabiBot.Api.Infrastructure.Database;
using WasabiBot.Api.Infrastructure.Discord;
using WasabiBot.DataAccess.Abstractions;
using WasabiBot.DataAccess.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.WebHost.UseKestrel(options => options.AddServerHeader = false);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Services.AddDiscordInfrastructure();
builder.AddAIInfrastructure();
builder.AddDatabase();
builder.AddServiceDefaults();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IInteractionService, InteractionService>();
builder.Services.AddTickerQ();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapDiscordCommands();
app.UseTickerQ();

app.MapGet("/", () => "Hello, world!");

app.Run();
