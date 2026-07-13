#:sdk Aspire.AppHost.Sdk@13.4.6
#:package Aspire.Hosting.PostgreSQL
#:package CommunityToolkit.Aspire.Hosting.Rust
#:property TargetFramework=net10.0
#:property RollForward=Major
#:property UserSecretsId=e740d40c-c13c-443b-a0cf-73ed8ab1c695

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

builder.Configuration.AddInMemoryCollection(
[
    new KeyValuePair<string, string?>("Logging:LogLevel:Microsoft.AspNetCore", "Warning"),
    new KeyValuePair<string, string?>("Logging:LogLevel:Aspire.Hosting.Dcp", "Warning"),
    new KeyValuePair<string, string?>("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL", "http://localhost:18889"),
    new KeyValuePair<string, string?>("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true")
]);

var discordBotToken = builder.AddParameter("discord-bot-token", secret: true)
    .WithDescription("Discord Bot Token");

var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent);

postgres.WithPgWeb(pgWeb => pgWeb.WithParentRelationship(postgres));

var database = postgres.AddDatabase("wasabi-db", "wasabi_db");

var migrations = builder.AddRustApp("migrations", "src-rs/wasabi-bot", args: ["--bin", "migrate"])
    .WithEnvironment("ConnectionStrings__wasabi_db", database.Resource.ConnectionStringExpression)
    .WaitFor(database)
    .WithParentRelationship(postgres);

builder.AddRustApp("wasabi-bot", "src-rs/wasabi-bot")
    .WithHttpEndpoint(env: "PORT")
    .WithHttpHealthCheck("/health")
    .WithEnvironment("ConnectionStrings__wasabi_db", database.Resource.ConnectionStringExpression)
    .WithEnvironment("Discord__Token", discordBotToken)
    .WithOtlpExporter()
    .WaitFor(database)
    .WaitForCompletion(migrations);

builder.Build().Run();
