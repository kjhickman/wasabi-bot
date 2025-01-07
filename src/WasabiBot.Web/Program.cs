using WasabiBot.Discord;
using WasabiBot.Web;
using WasabiBot.Web.Endpoints;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.ConfigureOpenTelemetry();

// builder.AddMessageHandlers();
// var connectionString = builder.Configuration.GetConnectionString("Postgres");
// builder.Services.AddTransient<IDbConnection>(_ => new NpgsqlConnection(connectionString));
// builder.Services.AddScoped<InteractionRecordService>();
// builder.Services.AddScoped<IInteractionService, InteractionService>();
// builder.Services.AddScoped<IDiscordService, DiscordService>();
// builder.Services.AddCommands();
// builder.Services.AddHttpClient();

builder.Services.AddDiscord(builder.Configuration);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, WebJsonContext.Default);
});

var app = builder.Build();

app.MapGet("/", () => "Hello, world!");

var v1 = app.MapGroup("/v1");
v1.MapEndpoints();

Console.WriteLine("Starting app...");

app.Run();