using dotenv.net;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

DotEnv.Load(new DotEnvOptions(envFilePaths: ["../../.env", "./.env"]));
builder.Configuration.AddEnvironmentVariables();

var postgres = builder.AddPostgres("postgres", password: builder.AddParameter("pgPassword"))
    .WithLifetime(ContainerLifetime.Persistent)
    .PublishAsConnectionString()
    .AddDatabase("wasabiBotDb", "wasabi_bot");

var migrations = builder.AddProject<Projects.WasabiBot_MigrationsRunner>("migrations")
    .WithReference(postgres)
    .WaitFor(postgres);

var server = builder.AddProject<Projects.WasabiBot_Web>("server")
    .WithExternalHttpEndpoints()
    .WithReference(postgres)
    .WithEndpoint(scheme: "http")
    .WaitForCompletion(migrations);


var ngrok = builder.AddContainer("ngrok", "ngrok/ngrok")
    .WithReference(server)
    .WithEnvironment("NGROK_AUTHTOKEN", builder.Configuration["NGROK_AUTHTOKEN"])
    .WithArgs("http", server.GetEndpoint("http"), $"--url={builder.Configuration["NGROK_DOMAIN"]}")
    .WaitFor(server);

builder.Build().Run();
