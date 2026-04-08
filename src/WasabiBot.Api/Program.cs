using WasabiBot.Api.Frontend;
using WasabiBot.Api.Features.Routing;
using Microsoft.Extensions.Caching.Hybrid;
using WasabiBot.Api.Infrastructure.AI;
using WasabiBot.Api.Infrastructure.Database;
using WasabiBot.Api.Infrastructure.Discord;
using WasabiBot.Api.Infrastructure.Lavalink;
using WasabiBot.Api.Infrastructure.Auth;
using WasabiBot.Api.Infrastructure.Scalar;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseKestrel(options => options.AddServerHeader = false);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.AddOpenApi();
builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Services.AddDiscordServices();
builder.AddLavalinkServices();
builder.AddAuthServices();
builder.AddAIServices();
builder.AddDbContext();
builder.AddServiceDefaults();
builder.Services.AddHybridCache(options =>
{
    options.MaximumPayloadBytes = 1024 * 1024;
    options.MaximumKeyLength = 1024;
});
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapScalarUi();
app.MapDefaultEndpoints();
app.MapEndpoints();
app.MapDiscordCommandHandlers();

app.Run();
