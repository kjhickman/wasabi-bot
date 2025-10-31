using AspNet.Security.OAuth.Discord;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
builder.AddAIServices();
builder.AddDbContext();
builder.AddServiceDefaults();

var discordAuthSection = builder.Configuration.GetSection("Discord");
var discordClientId = discordAuthSection["ClientId"];
var discordClientSecret = discordAuthSection["ClientSecret"];

if (string.IsNullOrWhiteSpace(discordClientId) || string.IsNullOrWhiteSpace(discordClientSecret))
{
    throw new InvalidOperationException("Discord OAuth configuration is missing. Set Authentication:Discord:ClientId and Authentication:Discord:ClientSecret.");
}

var discordCallbackPath = discordAuthSection["CallbackPath"];

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = DiscordAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/signin-discord";
    })
    .AddDiscord(options =>
    {
        options.ClientId = discordClientId;
        options.ClientSecret = discordClientSecret;
        options.CallbackPath = string.IsNullOrWhiteSpace(discordCallbackPath)
            ? new PathString("/signin-discord-callback")
            : new PathString(discordCallbackPath);
        if (!options.Scope.Contains("identify"))
        {
            options.Scope.Add("identify");
        }
        if (!options.Scope.Contains("guilds"))
        {
            options.Scope.Add("guilds");
        }
        options.SaveTokens = true;
    });

builder.Services.AddSingleton<IAuthorizationHandler, DiscordGuildRequirementHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("DiscordGuildMember", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new DiscordGuildRequirement());
    });
});

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
app.MapGet("/signin-discord", (string? returnUrl) =>
    {
        var redirectUri = "/";
        if (!string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith("/", StringComparison.Ordinal))
        {
            redirectUri = returnUrl;
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUri
        };

        return Results.Challenge(properties, new[] { DiscordAuthenticationDefaults.AuthenticationScheme });
    })
    .AllowAnonymous();

app.MapPost("/signout", async (HttpContext httpContext) =>
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    })
    .RequireAuthorization();

app.MapGet("/", () => "Hello, world!").RequireAuthorization("DiscordGuildMember");

app.Run();
