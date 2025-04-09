using WasabiBot.Api.DependencyInjection;

var builder = WebApplication.CreateSlimBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Services.AddDiscord();
builder.AddAIServices();
builder.AddServiceDefaults();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapDiscordCommands();

app.Run();

