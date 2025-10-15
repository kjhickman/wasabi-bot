var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgWeb();

var database = postgres.AddDatabase("wasabi-db");

var api = builder.AddProject<Projects.WasabiBot_Api>("wasabi-bot")
    .WithReference(database).WaitFor(postgres);

var migrations = builder.AddProject<Projects.WasabiBot_Migrations>("migrations")
    .WithReference(database)
    .WaitFor(database);

builder.Build().Run();
