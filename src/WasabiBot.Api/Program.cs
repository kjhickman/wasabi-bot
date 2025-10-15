using Microsoft.EntityFrameworkCore;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.DependencyInjection;
using WasabiBot.Api.Infrastructure.AI;
using WasabiBot.Api.Infrastructure.Discord;
using WasabiBot.DataAccess;
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
builder.AddServiceDefaults();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IInteractionService, InteractionService>();

builder.Services.AddDbContext<WasabiBotContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("wasabi-db"));
});

builder.Services.AddTickerQ(o =>
{
    o.SetMaxConcurrency(0); // Will use Environment.ProcessorCount
    o.AddOperationalStore<WasabiBotContext>(ef =>
    {
        ef.UseModelCustomizerForMigrations();
    });
});

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapDiscordCommands();
app.UseTickerQ();

app.MapGet("/", () => "Hello, world!");

app.Run();
