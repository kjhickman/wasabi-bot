using Microsoft.OpenApi;
using Scalar.AspNetCore;
using WasabiBot.Api.Infrastructure.Auth;

namespace WasabiBot.Api.Infrastructure.Scalar;

internal static class ScalarConfiguration
{
    private const string ApiTokenSchemeName = "ApiToken";

    public static void AddOpenApi(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??=
                    new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.OrdinalIgnoreCase);
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
    }

    public static void MapScalarUi(this WebApplication app)
    {
        app.MapOpenApi();

        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        var tokenFactory = app.Services.GetRequiredService<ApiTokenFactory>();

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
}
