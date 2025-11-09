using Microsoft.AspNetCore.HttpOverrides;
using WasabiBot.Api.Core.Extensions;
using WasabiBot.Api.Features.Routing;
using WasabiBot.Api.Infrastructure.AI;
using WasabiBot.Api.Infrastructure.Database;
using WasabiBot.Api.Infrastructure.Discord;
using WasabiBot.Api.Infrastructure.Auth;
using WasabiBot.Api.Infrastructure.Scalar;

var builder = WebApplication.CreateSlimBuilder(args);

builder.WebHost.UseKestrel(options => options.AddServerHeader = false);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.AddOpenApi();
builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Services.AddDiscordServices();
builder.AddAuthServices();
builder.AddAIServices();
builder.AddDbContext();
builder.AddServiceDefaults();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders(); // May be able to remove once using custom domain
var configuredPathBase = app.Configuration["ASPNETCORE_PATHBASE"];
if (!configuredPathBase.IsNullOrWhiteSpace())
{
    app.Logger.LogInformation("Applying path base {PathBase}", configuredPathBase);
    app.UsePathBase(configuredPathBase);
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapScalarUi();
app.MapDefaultEndpoints();
app.MapDiscordCommandHandlers();
app.MapEndpoints();

app.Run();
