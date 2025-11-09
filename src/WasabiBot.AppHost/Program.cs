using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgWeb();

var database = postgres.AddDatabase("wasabi-db");

var api = builder.AddProject<Projects.WasabiBot_Api>("wasabi-bot")
    .WithReference(database)
    .WaitFor(database)
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
