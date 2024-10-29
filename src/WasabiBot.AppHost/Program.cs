var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres", password: builder.AddParameter("pgPassword"))
    .PublishAsConnectionString()
    .AddDatabase("wasabiBotDb", "wasabi_bot");
    // .WithLifetime(ContainerLifetime.Persistent);

var migrations = builder.AddProject<Projects.WasabiBot_MigrationsRunner>("migrations")
    .WithReference(postgres)
    .WaitFor(postgres);

var server = builder.AddProject<Projects.WasabiBot_Web>("server")
    .WithReference(postgres)
    .WaitFor(migrations);

builder.Build().Run();