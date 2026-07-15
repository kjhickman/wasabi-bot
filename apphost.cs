#:sdk Aspire.AppHost.Sdk@13.4.6
#:package Aspire.Hosting.PostgreSQL@13.4.6
#:package CommunityToolkit.Aspire.Hosting.Rust@13.4.0
#:property TargetFramework=net10.0
#:property RollForward=Major
#:property UserSecretsId=e740d40c-c13c-443b-a0cf-73ed8ab1c695

using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

builder.Configuration.AddInMemoryCollection(
[
    new KeyValuePair<string, string?>("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL", "http://localhost:18889"),
    new KeyValuePair<string, string?>("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true")
]);

var discordBotToken = builder.AddParameter("discord-bot-token", secret: true)
    .WithDescription("Discord Bot Token");

var googleApiKey = builder.AddParameter("google-api-key", secret: true)
    .WithDescription("Google API Key");

var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent);

postgres.WithPgWeb(pgWeb => pgWeb.WithParentRelationship(postgres));

var database = postgres.AddDatabase("wasabi-db", "wasabi_db");

var lavalink = builder.AddContainer("lavalink", "ghcr.io/lavalink-devs/lavalink", "4.2.2-alpine")
    .WithHttpEndpoint(port: 2333, targetPort: 2333, name: "http")
    .WithContainerFiles("/opt/Lavalink", "./lavalink")
    .WithEnvironment("SERVER_PORT", "2333")
    .WithLifetime(ContainerLifetime.Persistent);

var migrations = builder.AddRustApp("migrations", ".", args: ["--bin", "migrate"])
    .WithEnvironment("DATABASE_URL", database.Resource.UriExpression)
    .WaitFor(database)
    .WithParentRelationship(postgres);

builder.AddRustApp("wasabi-bot", ".")
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("DATABASE_URL", database.Resource.UriExpression)
    .WithEnvironment("DISCORD_TOKEN", discordBotToken)
    .WithEnvironment("GEMINI_API_KEY", googleApiKey)
    .WithEnvironment("LAVALINK_URL", lavalink.GetEndpoint("http"))
    .WithOtlpExporter()
    .WaitFor(database)
    .WaitFor(lavalink)
    .WaitForCompletion(migrations);

builder.Build().Run();
