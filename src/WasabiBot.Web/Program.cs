using System.Data;
using System.Reflection;
using Npgsql;
using WasabiBot.DataAccess.Repositories;
using WasabiBot.Discord;
using WasabiBot.Web.Endpoints;
using WasabiBot.Web.Middleware;
using WasabiBot.Web.Services;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddUserSecrets(Assembly.GetEntryAssembly()!)
    .AddEnvironmentVariables();

builder.ConfigureOpenTelemetry();
var connectionString = builder.Configuration.GetConnectionString("Postgres");
builder.Services.AddTransient<IDbConnection>(_ => new NpgsqlConnection(connectionString));
builder.Services.AddScoped<InteractionRecordRepository>();
builder.Services.AddDiscord(builder.Configuration);
builder.Services.AddHostedService<BackgroundInteractionService>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

app.UseMiddleware<ResponseTimeMiddleware>();

await app.Services.InitializeDiscordAsync();

var v1 = app.MapGroup("/v1");
v1.MapEndpoints();

Console.WriteLine("Starting app...");

app.Run();