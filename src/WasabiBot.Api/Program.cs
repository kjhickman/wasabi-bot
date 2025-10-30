using WasabiBot.Api.Core.Extensions;
using WasabiBot.Api.Infrastructure.AI;
using WasabiBot.Api.Infrastructure.Database;
using WasabiBot.Api.Infrastructure.Discord;

var builder = WebApplication.CreateSlimBuilder(args);

builder.WebHost.UseKestrel(options => options.AddServerHeader = false);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Services.AddDiscordServices();
builder.AddAIServices();
builder.AddDbContext();
builder.AddServiceDefaults();

var app = builder.Build();

var configuredPathBase = app.Configuration["ASPNETCORE_PATHBASE"];
if (!configuredPathBase.IsNullOrWhiteSpace())
{
    app.Logger.LogInformation("Applying path base {PathBase}", configuredPathBase);
    app.UsePathBase(configuredPathBase);
}

app.MapDefaultEndpoints();
app.MapDiscordCommandHandlers();
app.MapGet("/", () => "Hello, world!");

app.Run();
