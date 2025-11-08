using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using WasabiBot.Api.Core.Extensions;
using WasabiBot.Api.Infrastructure.AI;
using WasabiBot.Api.Infrastructure.Database;
using WasabiBot.Api.Infrastructure.Discord;
using WasabiBot.Api.Infrastructure.Auth;

var builder = WebApplication.CreateSlimBuilder(args);
const string ApiTokenSchemeName = "ApiToken";

builder.WebHost.UseKestrel(options => options.AddServerHeader = false);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        if (!document.Components.SecuritySchemes.ContainsKey(ApiTokenSchemeName))
        {
            document.Components.SecuritySchemes[ApiTokenSchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Name = "Authorization",
                Description = "Issue a token via /token after signing in with Discord, then Scalar will send it as a Bearer header."
            };
        }

        return Task.CompletedTask;
    });
});
builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Services.AddDiscordServices();
builder.AddAuthServices();
builder.AddAIServices();
builder.AddDbContext();
builder.AddServiceDefaults();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    var tokenFactory = app.Services.GetRequiredService<ApiTokenFactory>();

    app.MapOpenApi();
    app.MapScalarApiReference((options, httpContext) =>
    {
        var hasToken = tokenFactory.TryCreateToken(httpContext.User, out var token);

        options.WithTitle("WasabiBot API");
        options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
        options.AddPreferredSecuritySchemes(ApiTokenSchemeName);
        options.AddHttpAuthentication(ApiTokenSchemeName, auth =>
        {
            if (hasToken)
            {
                auth.Token = token;
            }
        });
        options.EnablePersistentAuthentication();
    });
}

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

app.MapGet("token-test", () => "If you see this, your token is valid!")
    .RequireAuthorization("ApiToken");

app.Run();
