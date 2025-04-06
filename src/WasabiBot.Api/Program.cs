using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration.AddUserSecrets<Program>(optional: true);

builder.Services.AddDiscordGateway();
builder.Services.AddApplicationCommands();

builder.AddServiceDefaults();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");
app.MapDefaultEndpoints();

app.AddSlashCommand("ping", "Ping!", () => "Pong!");
app.UseGatewayEventHandlers();

app.Run();

