using System.Data;
using Npgsql;
using WasabiBot.Api.DependencyInjection;
using WasabiBot.DataAccess.Interfaces;
using WasabiBot.DataAccess.Repositories;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Services.AddDiscord();
builder.AddAIServices();
builder.AddServiceDefaults();

builder.Services.AddScoped<IInteractionRepository, InteractionRepository>();

var connectionString = builder.Configuration.GetConnectionString("wasabi-db");
builder.Services.AddTransient<IDbConnection>(_ => new NpgsqlConnection(connectionString));

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapDiscordCommands();

app.Run();

