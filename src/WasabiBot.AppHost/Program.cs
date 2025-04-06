var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.WasabiBot_Api>("api");

builder.Build().Run();
