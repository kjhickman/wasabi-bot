#:sdk Aspire.AppHost.Sdk@13.2.1
#:package Aspire.Hosting.PostgreSQL
#:property UserSecretsId=e740d40c-c13c-443b-a0cf-73ed8ab1c695

using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

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

var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent);

postgres.WithPgWeb(pgWeb => pgWeb.WithParentRelationship(postgres));

var database = postgres.AddDatabase("wasabi-db", "wasabi_db");

var migrations = builder.AddProject("migrations", "src/WasabiBot.Migrations/WasabiBot.Migrations.csproj")
    .WithReference(database)
    .WithEnvironment("ConnectionStrings__wasabi_db", database.Resource.ConnectionStringExpression)
    .WaitFor(database)
    .WithParentRelationship(postgres);

var api = builder.AddProject("wasabi-bot", "src/WasabiBot.Api/WasabiBot.Api.csproj")
    .WithReference(database)
    .WithEnvironment("ConnectionStrings__wasabi_db", database.Resource.ConnectionStringExpression)
    .WaitFor(database)
    .WithEnvironment("Authentication__Discord__ClientId", discordClientId)
    .WithEnvironment("Authentication__Discord__ClientSecret", discordClientSecret)
    .WithEnvironment("Discord__Token", discordBotToken)
    .WithEnvironment("OpenRouterV2__ApiKey", openRouterKey)
    .WithUrlForEndpoint("http", url => url.DisplayText = "Frontend")
    .WithUrlForEndpoint("http", _ => new ResourceUrlAnnotation
    {
        Url = "/scalar/v1",
        DisplayText = "API Reference"
    });

builder.Build().Run();
