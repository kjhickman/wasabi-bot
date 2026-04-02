using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace WasabiBot.Api.Infrastructure.Scalar;

internal static class ScalarConfiguration
{
    private const string ApiTokenSchemeName = "ApiToken";
    private const string OAuthTokenPath = "/api/v1/oauth/token";

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
                        Type = SecuritySchemeType.OAuth2,
                        Description = "Exchange API client credentials for a JWT, then Scalar will send it as a Bearer header on subsequent requests.",
                        Flows = new OpenApiOAuthFlows
                        {
                            ClientCredentials = new OpenApiOAuthFlow
                            {
                                TokenUrl = new Uri(OAuthTokenPath, UriKind.Relative),
                                Scopes = new Dictionary<string, string>()
                            }
                        }
                    };
                }

                return Task.CompletedTask;
            });
        });
    }

    public static void MapScalarUi(this WebApplication app)
    {
        app.MapOpenApi();

        app.MapScalarApiReference(options =>
        {
            options.WithTitle("WasabiBot API");
            options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
            options.AddPreferredSecuritySchemes(ApiTokenSchemeName);
            options.AddClientCredentialsFlow(ApiTokenSchemeName, flow =>
            {
                flow.WithTokenUrl(OAuthTokenPath);
                flow.WithClientId(string.Empty);
                flow.WithClientSecret(string.Empty);
                flow.WithCredentialsLocation(CredentialsLocation.Body);
                flow.AddBodyParameter("grant_type", "client_credentials");
                flow.WithTokenName("access_token");
            });
            options.EnablePersistentAuthentication();
        }).RequireAuthorization("DiscordGuildMember");
    }
}
