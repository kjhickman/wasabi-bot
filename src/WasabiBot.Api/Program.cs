using WasabiBot.Api.Frontend;
using WasabiBot.Api.Features.Routing;
using WasabiBot.Api.Infrastructure.Discord;
using WasabiBot.Api.Core.Serialization;
using WasabiBot.Api.Infrastructure;
using WasabiBot.Api.Features;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddUserSecrets<Program>(optional: true);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, JsonContext.Default);
});

// Application services
builder.AddServiceDefaults();
builder.AddInfrastructure();
builder.AddFeatures();

// Frontend services
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
