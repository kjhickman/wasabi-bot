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

var guildId = app.Configuration.GetValue<ulong?>("Discord:TestGuildId");
app.AddSlashCommand("ping", "Ping!", () => "Pong!", guildId: guildId);
app.UseGatewayEventHandlers();

app.Run();

