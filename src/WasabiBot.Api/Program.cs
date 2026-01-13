using WasabiBot.Api.Components;
using WasabiBot.Api.Features.Routing;
using WasabiBot.Api.Infrastructure.AI;
using WasabiBot.Api.Infrastructure.Database;
using WasabiBot.Api.Infrastructure.Discord;
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
if (!builder.Environment.IsDevelopment())
{
    builder.Configuration.AddSystemsManager(options =>
    {
        options.Path = $"/wasabi-bot/{builder.Environment.EnvironmentName}";
        options.ReloadAfter = TimeSpan.FromMinutes(1);
    });
}
builder.Services.AddDiscordServices();
builder.AddAuthServices();
builder.AddAIServices();
builder.AddDbContext();
builder.AddServiceDefaults();
builder.Services.AddRazorComponents();
builder.Services.AddCascadingAuthenticationState();

var app = builder.Build();

app.MapStaticAssets();
app.MapRazorComponents<App>();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapScalarUi();
app.MapDefaultEndpoints();
app.MapEndpoints();
app.MapDiscordCommandHandlers();

app.Run();
