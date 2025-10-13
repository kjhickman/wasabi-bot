using Microsoft.EntityFrameworkCore;
using WasabiBot.Api.Infrastructure.AI;
using WasabiBot.Api.Infrastructure.Discord;
using WasabiBot.DataAccess;
using WasabiBot.DataAccess.Interfaces;
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
builder.AddServiceDefaults();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IInteractionService, InteractionService>();
// builder.Services.AddScoped<IReminderService, ReminderService>();

// builder.Services.AddHostedService<ReminderProcessor>();

builder.Services.AddDbContext<WasabiBotContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("wasabi-db"));
});

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapDiscordCommands();

app.MapGet("/", () => "Hello, world!");

app.Run();
