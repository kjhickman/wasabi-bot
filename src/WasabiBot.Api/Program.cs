using WasabiBot.Api.Frontend;
using WasabiBot.Api.Features.Routing;
using WasabiBot.Api.Infrastructure.AI;
using WasabiBot.Api.Infrastructure.Discord;
using WasabiBot.Api.Infrastructure.Auth;
using WasabiBot.Api.Core.Serialization;
using WasabiBot.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseKestrel(options => options.AddServerHeader = false);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, JsonContext.Default);
});

builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Services.AddDiscordServices();
builder.AddAuthServices();
builder.AddAIServices();
builder.AddInfrastructure();
builder.AddServiceDefaults();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

// Frontend
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Auth
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// API Endpoints / Handlers
app.MapOpenApi();
app.MapDefaultEndpoints();
app.MapEndpoints();
app.MapDiscordCommandHandlers();

app.Run();
