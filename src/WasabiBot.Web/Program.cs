using System.Data;
using System.Reflection;
using Npgsql;
using WasabiBot.DataAccess.Services;
using WasabiBot.Discord;
using WasabiBot.Web.Endpoints;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddUserSecrets(Assembly.GetEntryAssembly()!)
    .AddEnvironmentVariables();

builder.ConfigureOpenTelemetry();
var connectionString = builder.Configuration.GetConnectionString("Postgres");
builder.Services.AddTransient<IDbConnection>(_ => new NpgsqlConnection(connectionString));
builder.Services.AddScoped<InteractionRecordService>();
builder.Services.AddDiscord(builder.Configuration);

var app = builder.Build();

await app.Services.InitializeDiscordAsync();

var v1 = app.MapGroup("/v1");
v1.MapEndpoints();

Console.WriteLine("Starting app...");

app.Run();