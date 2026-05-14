using Microsoft.Extensions.DependencyInjection;
using WasabiBot.Api.Core.Serialization;
using WasabiBot.Api.Features.ApiCredentials;
using WasabiBot.Api.Features.Routing;
using WasabiBot.Api.Infrastructure.AI;
using WasabiBot.Api.Infrastructure.Database;
using WasabiBot.Api.Infrastructure.Discord;
using WasabiBot.Api.Infrastructure.Lavalink;
using WasabiBot.Api.Infrastructure.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseKestrel(options => options.AddServerHeader = false);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.AddOpenApi();
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
})
    .AddSerializer(new JsonHybridCacheSerializer<ApiCredentialSummary[]>(JsonContext.Default.ApiCredentialSummaryArray))
    .AddSerializer(new JsonHybridCacheSerializer<CachedApiCredential>(JsonContext.Default.CachedApiCredential))
    .AddSerializer(new BooleanHybridCacheSerializer())
    .AddSerializer(new UInt64ArrayHybridCacheSerializer());

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapDefaultEndpoints();
app.MapEndpoints();
app.MapDiscordCommandHandlers();

app.Run();
