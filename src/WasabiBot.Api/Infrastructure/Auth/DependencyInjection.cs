using System.Text;
using AspNet.Security.OAuth.Discord;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using WasabiBot.Api.Core.Extensions;

namespace WasabiBot.Api.Infrastructure.Auth;

public static class DependencyInjection
{
    public static IServiceCollection AddAuthServices(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var services = builder.Services;

        var discordAuthSection = configuration.GetSection("Authentication:Discord");
        var discordClientId = discordAuthSection["ClientId"];
        var discordClientSecret = discordAuthSection["ClientSecret"];

        if (discordClientId.IsNullOrWhiteSpace() || discordClientSecret.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Discord OAuth configuration is missing.");
        }

        var discordCallbackPath = discordAuthSection["CallbackPath"];

        var tokenSection = configuration.GetSection("Authentication:Token");
        var tokenSigningKey = tokenSection["SigningKey"];
        if (builder.Environment.IsDevelopment())
        {
            tokenSigningKey ??= "DevelopmentSigningKeyMustBe32Characters!";
        }
        if (tokenSigningKey.IsNullOrWhiteSpace() || tokenSigningKey.Length < 32)
        {
            throw new InvalidOperationException("API token configuration is missing or invalid.");
        }

        var tokenLifetimeMinutes = configuration.GetValue<int?>("Authentication:Token:LifetimeMinutes") ?? 60;
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenSigningKey));
        var apiTokenOptions = new ApiTokenOptions(signingKey, TimeSpan.FromMinutes(tokenLifetimeMinutes));

        var apiCredentialSection = configuration.GetSection("Authentication:ApiCredentials");
        var apiCredentialPepper = apiCredentialSection["Pepper"];
        if (builder.Environment.IsDevelopment())
        {
            apiCredentialPepper ??= "DevelopmentApiCredentialPepperMustBe32Chars!";
        }
        if (apiCredentialPepper.IsNullOrWhiteSpace() || apiCredentialPepper.Length < 32)
        {
            throw new InvalidOperationException("API credential secret configuration is missing or invalid.");
        }

        var apiCredentialSecretOptions = new ApiCredentialSecretOptions(apiCredentialPepper);

        services.AddSingleton(apiTokenOptions);
        services.AddSingleton(apiCredentialSecretOptions);
        services.AddSingleton<ApiTokenFactory>();
        services.AddSingleton<IApiCredentialSecretService, ApiCredentialSecretService>();
        services.AddSingleton<IDiscordGuildAuthorizationClient, DiscordGuildAuthorizationClient>();
        services.AddScoped<IApiCredentialService, ApiCredentialService>();

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = DiscordAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/login-discord";
                options.Events.OnRedirectToLogin = context =>
                {
                    if (IsApiRequest(context.Request.Path))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect("/");
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    if (IsApiRequest(context.Request.Path))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect("/");
                    return Task.CompletedTask;
                };
            })
            .AddDiscord(options =>
            {
                options.ClientId = discordClientId;
                options.ClientSecret = discordClientSecret;
                options.CallbackPath = string.IsNullOrWhiteSpace(discordCallbackPath)
                    ? "/login-discord-callback"
                    : discordCallbackPath;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                };
            });

        services.AddSingleton<IAuthorizationHandler, DiscordGuildRequirementHandler>();
        services.AddAuthorizationBuilder()
            .AddPolicy("DiscordGuildMember", policy =>
            {
                policy.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new DiscordGuildRequirement());
            })
            .AddPolicy("ApiToken", policy =>
            {
                policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                policy.RequireAuthenticatedUser();
            });

        return services;
    }

    private static bool IsApiRequest(PathString path)
    {
        return path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
    }
}
