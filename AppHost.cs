#:sdk Aspire.AppHost.Sdk@13.3.2
#:package Aspire.Hosting.PostgreSQL
#:property UserSecretsId=e740d40c-c13c-443b-a0cf-73ed8ab1c695

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);
var runApiAsContainer = args.Contains("--api-container", StringComparer.OrdinalIgnoreCase);

builder.Configuration.AddInMemoryCollection(
[
    new KeyValuePair<string, string?>("Logging:LogLevel:Microsoft.AspNetCore", "Warning"),
    new KeyValuePair<string, string?>("Logging:LogLevel:Aspire.Hosting.Dcp", "Warning")
]);

var discordClientId = builder.AddParameter("discord-client-id")
    .WithDescription("Discord OAuth2 Client ID");

var discordClientSecret = builder.AddParameter("discord-client-secret", secret: true)
    .WithDescription("Discord OAuth2 Client Secret");

var discordBotToken = builder.AddParameter("discord-bot-token", secret: true)
    .WithDescription("Discord Bot Token");

var openRouterKey = builder.AddParameter("openrouter-api-key", secret: true)
    .WithDescription("OpenRouter API Key");

const string lavalinkImagePrefix = "FROM ghcr.io/lavalink-devs/lavalink:";

var lavalinkImageTag = File.ReadLines("infra/lavalink/Dockerfile")
    .First(line => line.StartsWith(lavalinkImagePrefix, StringComparison.Ordinal))[lavalinkImagePrefix.Length..];

var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent);

postgres.WithPgWeb(pgWeb => pgWeb.WithParentRelationship(postgres));

var database = postgres.AddDatabase("wasabi-db", "wasabi_db");

var migrations = builder.AddProject("migrations", "src/WasabiBot.Migrations/WasabiBot.Migrations.csproj")
    .WithReference(database)
    .WithEnvironment("ConnectionStrings__wasabi_db", database.Resource.ConnectionStringExpression)
    .WaitFor(database)
    .WithParentRelationship(postgres);

var lavalink = builder.AddContainer("lavalink", "ghcr.io/lavalink-devs/lavalink", lavalinkImageTag)
    .WithHttpEndpoint(port: 2333, targetPort: 2333, name: "http")
    .WithContainerFiles("/opt/Lavalink", "./infra/lavalink")
    .WithEnvironment("SERVER_PORT", "2333")
    .WithLifetime(ContainerLifetime.Persistent);

if (runApiAsContainer)
{
    ConfigureApi(builder.AddDockerfile("wasabi-bot", ".", "src/WasabiBot.Api/Dockerfile")
        .WithHttpEndpoint(targetPort: 8080, name: "http")
        .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development"));
}
else
{
    var frontendDependencies = builder.AddExecutable("frontend-deps", "bun", "src/WasabiBot.Api", "install", "--frozen-lockfile");

    var frontendCss = builder.AddExecutable("frontend-css", "bun", "src/WasabiBot.Api", "run", "build:css")
        .WaitForCompletion(frontendDependencies);

    ConfigureApi(builder.AddProject("wasabi-bot", "src/WasabiBot.Api/WasabiBot.Api.csproj")
        .WaitForCompletion(frontendCss));
}

IResourceBuilder<TResource> ConfigureApi<TResource>(IResourceBuilder<TResource> api)
    where TResource : IResourceWithEnvironment, IResourceWithWaitSupport
{
    return api
        .WithReference(database)
        .WithEnvironment("ConnectionStrings__wasabi_db", database.Resource.ConnectionStringExpression)
        .WaitFor(database)
        .WaitFor(lavalink)
        .WithEnvironment("Authentication__Discord__ClientId", discordClientId)
        .WithEnvironment("Authentication__Discord__ClientSecret", discordClientSecret)
        .WithEnvironment("Discord__Token", discordBotToken)
        .WithEnvironment("OpenRouterV2__ApiKey", openRouterKey)
        .WithEnvironment("Lavalink__BaseUrl", lavalink.GetEndpoint("http"))
        .WaitForCompletion(migrations)
        .WithUrls(context =>
        {
            var httpEndpoint = context.GetEndpoint("http");

            foreach (var url in context.Urls)
            {
                url.DisplayText = "Frontend";
            }

            context.Urls.Add(new ResourceUrlAnnotation
            {
                Url = "/openapi/v1.json",
                DisplayText = "OpenAPI Spec",
                Endpoint = httpEndpoint
            });
        });
}

builder.Build().Run();
