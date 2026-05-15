using Microsoft.OpenApi;

namespace WasabiBot.Api.Infrastructure.OpenApi;

internal static class OpenApiConfiguration
{
    private const string ApiTokenSchemeName = "ApiToken";
    private const string OAuthTokenPath = "/api/v1/oauth/token";

    public static void AddOpenApiSpec(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??=
                    new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.OrdinalIgnoreCase);

                document.Components.SecuritySchemes.TryAdd(ApiTokenSchemeName, new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Description = "Exchange API client credentials for a JWT, then send it as a Bearer header on subsequent requests.",
                    Flows = new OpenApiOAuthFlows
                    {
                        ClientCredentials = new OpenApiOAuthFlow
                        {
                            TokenUrl = new Uri(OAuthTokenPath, UriKind.Relative),
                            Scopes = new Dictionary<string, string>()
                        }
                    }
                });

                return Task.CompletedTask;
            });
        });
    }

    public static void MapOpenApiSpec(this WebApplication app)
    {
        app.MapOpenApi();
    }
}
