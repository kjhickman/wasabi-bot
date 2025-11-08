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

        services.AddSingleton(apiTokenOptions);
        services.AddSingleton<ApiTokenFactory>();

        services
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
                    ? "/signin-discord-callback"
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
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddSingleton<IAuthorizationHandler, DiscordGuildRequirementHandler>();
        services.AddAuthorizationBuilder()
            .AddPolicy("DiscordGuildMember", policy =>
            {
                policy.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme, JwtBearerDefaults.AuthenticationScheme);
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
}
