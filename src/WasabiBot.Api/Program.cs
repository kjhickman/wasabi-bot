using Microsoft.EntityFrameworkCore;
using WasabiBot.DataAccess;
using WasabiBot.DataAccess.Interfaces;
using WasabiBot.DataAccess.Services;
using WasabiBot.Api.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.WebHost.UseKestrel(options => options.AddServerHeader = false);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Services.AddDiscord();
builder.AddAIServices();
builder.AddServiceDefaults();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IInteractionService, InteractionService>();
builder.Services.AddScoped<IReminderService, ReminderService>();

builder.Services.AddHostedService<ReminderDispatcher>();

builder.Services.AddDbContext<WasabiBotContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("wasabi-db"));
});

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapDiscordCommands();

app.MapGet("/", () => "Hello, world!");

app.Run();
