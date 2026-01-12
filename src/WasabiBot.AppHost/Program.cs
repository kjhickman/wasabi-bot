var builder = DistributedApplication.CreateBuilder(args);

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

var database = postgres.AddDatabase("wasabi-db");

var migrations = builder.AddProject<Projects.WasabiBot_Migrations>("migrations")
    .WithReference(database)
    .WaitFor(database).WithParentRelationship(postgres);

var api = builder.AddProject<Projects.WasabiBot_Api>("wasabi-bot")
    .WithReference(database)
    .WaitFor(database)
    .WithEnvironment("Authentication__Discord__ClientId", discordClientId)
    .WithEnvironment("Authentication__Discord__ClientSecret", discordClientSecret)
    .WithEnvironment("Discord__Token", discordBotToken)
    .WithEnvironment("OpenRouter__ApiKey", openRouterKey)
    .WithUrlForEndpoint("http", url => url.DisplayText = "Frontend")
    .WithUrlForEndpoint("http", _ => new ResourceUrlAnnotation
    {
        Url = "/scalar",
        DisplayText = "API Reference"
    });

builder.Build().Run();
