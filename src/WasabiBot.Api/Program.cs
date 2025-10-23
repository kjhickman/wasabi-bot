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

var app = builder.Build();

var configuredPathBase = app.Configuration["ASPNETCORE_PATHBASE"];
if (!string.IsNullOrWhiteSpace(configuredPathBase))
{
    app.Logger.LogInformation("Applying path base {PathBase}", configuredPathBase);
    app.UsePathBase(configuredPathBase);
}

app.MapDefaultEndpoints();
app.MapDiscordCommands();
app.MapGet("/", () => "Hello, world!");

app.Run();
