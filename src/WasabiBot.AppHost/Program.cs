var builder = DistributedApplication.CreateBuilder(args);

var discordClientId = builder.AddParameter("discord-client-id")
    .WithDescription("Discord OAuth2 Client ID");

var discordClientSecret = builder.AddParameter("discord-client-secret", secret: true)
    .WithDescription("Discord OAuth2 Client Secret");

var discordBotToken = builder.AddParameter("discord-bot-token", secret: true)
    .WithDescription("Discord Bot Token");

var geminiApiKey = builder.AddParameter("gemini-api-key", secret: true)
    .WithDescription("Gemini API Key for AI features");

var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgWeb();

var database = postgres.AddDatabase("wasabi-db");

var api = builder.AddProject<Projects.WasabiBot_Api>("wasabi-bot")
    .WithReference(database)
    .WaitFor(database)
    .WithEnvironment("Authentication__Discord__ClientId", discordClientId)
    .WithEnvironment("Authentication__Discord__ClientSecret", discordClientSecret)
    .WithEnvironment("Discord__Token", discordBotToken)
    .WithEnvironment("AI__GeminiApiKey", geminiApiKey)
    .WithChildRelationship(discordClientId)
    .WithChildRelationship(discordClientSecret)
    .WithChildRelationship(discordBotToken)
    .WithChildRelationship(geminiApiKey)
    .WithUrlForEndpoint("http", url => url.DisplayText = "Frontend")
    .WithUrlForEndpoint("http", _ => new ResourceUrlAnnotation
    {
        Url = "/scalar",
        DisplayText = "API Reference"
    });

var migrations = builder.AddProject<Projects.WasabiBot_Migrations>("migrations")
    .WithReference(database)
    .WaitFor(database);

builder.Build().Run();
