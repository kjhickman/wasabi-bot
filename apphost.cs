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

var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent);

postgres.WithPgWeb(pgWeb => pgWeb.WithParentRelationship(postgres));

var database = postgres.AddDatabase("wasabi-db", "wasabi_db");

var migrations = builder.AddRustApp("migrations", ".", args: ["--bin", "migrate"])
    .WithEnvironment("DATABASE_URL", database.Resource.UriExpression)
    .WaitFor(database)
    .WithParentRelationship(postgres);

builder.AddRustApp("wasabi-bot", ".")
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("DATABASE_URL", database.Resource.UriExpression)
    .WithEnvironment("DISCORD_TOKEN", discordBotToken)
    .WithOtlpExporter()
    .WaitFor(database)
    .WaitForCompletion(migrations);

builder.Build().Run();
