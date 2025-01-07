using WasabiBot.Discord;
using WasabiBot.Web.Endpoints;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.ConfigureOpenTelemetry();
builder.Services.AddDiscord(builder.Configuration);

var app = builder.Build();

app.MapGet("/", () => "Hello, world!");

var v1 = app.MapGroup("/v1");
v1.MapEndpoints();

Console.WriteLine("Starting app...");

app.Run();