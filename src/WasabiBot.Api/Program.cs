using WasabiBot.Api.Core.Extensions;
using WasabiBot.Api.Infrastructure.AI;
using WasabiBot.Api.Infrastructure.Database;
using WasabiBot.Api.Infrastructure.Discord;
using WasabiBot.Api.Infrastructure.Auth;

var builder = WebApplication.CreateSlimBuilder(args);

builder.WebHost.UseKestrel(options => options.AddServerHeader = false);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Services.AddDiscordServices();
builder.AddAuthServices();
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

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapDiscordCommandHandlers();
app.MapAuthEndpoints();

app.MapGet("/", (HttpContext ctx) =>
    {
        var user = ctx.User;
        return $"Hello, {user.Identity!.Name}!";
    })
    .RequireAuthorization("DiscordGuildMember");

app.Run();
