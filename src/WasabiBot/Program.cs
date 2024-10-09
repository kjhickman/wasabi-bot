using System.Data;
using Amazon.SQS;
using dotenv.net;
using Npgsql;
using Serilog;
using Serilog.Formatting.Compact;
using WasabiBot;
using WasabiBot.Commands;
using WasabiBot.Endpoints;
using WasabiBot.Interfaces;
using WasabiBot.Services;
using WasabiBot.Settings;

var builder = WebApplication.CreateSlimBuilder(args);

DotEnv.Load(); // todo: only run if development
builder.Configuration.AddEnvironmentVariables();
builder.Services.Configure<EnvironmentVariables>(builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, JsonContext.Default);
});
builder.Services.AddScoped<IInteractionService, InteractionService>();
builder.Services.AddScoped<IDiscordService, DiscordService>();
builder.Services.AddHttpClient();
builder.Services.AddCommandHandlers();
builder.Services.AddSingleton<IAmazonSQS, AmazonSQSClient>();
builder.Services.AddTransient<IDbConnection>(_ => new NpgsqlConnection(builder.Configuration.GetConnectionString("Postgres")));
builder.Services.AddScoped<InteractionRecordService>();

builder.Logging.ClearProviders();
ILogger logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateLogger();
Log.Logger = logger;
builder.Services.AddSingleton(logger);

var app = builder.Build();

app.MapGet("/", () => "Hello, world!");
app.MapDiscordEndpoints();
app.MapEventEndpoints();

app.Run();
