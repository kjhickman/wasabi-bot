var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume();

var database = postgres.AddDatabase("wasabi-db");
var api = builder.AddProject<Projects.WasabiBot_Api>("wasabi-bot")
    .WithReference(database);

var migrations = builder.AddProject<Projects.WasabiBot_Migrations>("migrations")
    .WithReference(database);

builder.Build().Run();
