using System.Data;
using Npgsql;
using OpenTelemetry.Trace;
using WasabiBot.Web;
using WasabiBot.Web.Commands;
using WasabiBot.Core.Interfaces;
using WasabiBot.DataAccess.Services;
using WasabiBot.Web.DependencyInjection;
using WasabiBot.Web.Endpoints;
using WasabiBot.Web.Services;
using WasabiBot.Web.Settings;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration.AddEnvironmentVariables();
builder.Services.Configure<DiscordSettings>(builder.Configuration.GetSection("Discord"));

// Add service defaults & Aspire components.
builder.AddServiceDefaults();
builder.Services.AddSingleton(TracerProvider.Default.GetTracer("wasabi_bot"));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, WebJsonContext.Default);
});

var connectionString = builder.Configuration.GetConnectionString("wasabiBotDb"); // TODO: fixme
builder.Services.AddTransient<IDbConnection>(_ => new NpgsqlConnection(connectionString));
builder.Services.AddScoped<InteractionRecordService>();
builder.Services.AddScoped<IInteractionService, InteractionService>();
builder.Services.AddScoped<IDiscordService, DiscordService>();
builder.Services.AddCommands();
builder.Services.AddHttpClient();
builder.AddMassTransit();

var app = builder.Build();

app.MapGet("/", () => "Hello, world!");

var v1 = app.MapGroup("/v1");
v1.MapEndpoints();

app.Run();