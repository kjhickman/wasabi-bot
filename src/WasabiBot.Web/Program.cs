using System.Data;
using Amazon;
using Amazon.Runtime;
using Amazon.RuntimeDependencies;
using Amazon.SecurityToken;
using Amazon.SQS;
using Npgsql;
using WasabiBot.Web;
using WasabiBot.Web.Commands;
using WasabiBot.Core.Interfaces;
using WasabiBot.DataAccess.Services;
using WasabiBot.Web.DependencyInjection;
using WasabiBot.Web.Endpoints;
using WasabiBot.Web.Services;
using WasabiBot.Web.Settings;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.ConfigureOpenTelemetry();
builder.AddMessageHandlers();

var connectionString = builder.Configuration.GetConnectionString("Postgres");
builder.Services.AddTransient<IDbConnection>(_ => new NpgsqlConnection(connectionString));
builder.Services.AddScoped<InteractionRecordService>();
builder.Services.AddScoped<IInteractionService, InteractionService>();
builder.Services.AddScoped<IDiscordService, DiscordService>();
builder.Services.AddCommands();
builder.Services.AddHttpClient();
builder.Services.Configure<DiscordSettings>(builder.Configuration.GetSection("Discord"));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, WebJsonContext.Default);
});

// Add AWS Services
if (!builder.Environment.IsDevelopment())
{
    var stsConfig = new AmazonSecurityTokenServiceConfig
    {
        RegionEndpoint = RegionEndpoint.USEast1
    };

    var webIdentityCredentials = new AssumeRoleWithWebIdentityCredentials(
        roleArn: Environment.GetEnvironmentVariable("AWS_ROLE_ARN")!,
        webIdentityTokenFile: Environment.GetEnvironmentVariable("AWS_WEB_IDENTITY_TOKEN_FILE")!,
        roleSessionName: Environment.GetEnvironmentVariable("AWS_ROLE_SESSION_NAME")!
    );

    GlobalRuntimeDependencyRegistry.Instance.RegisterSecurityTokenServiceClient(new AmazonSecurityTokenServiceClient(webIdentityCredentials, stsConfig));
    
    builder.Services.AddSingleton<IAmazonSQS>(new AmazonSQSClient(webIdentityCredentials, RegionEndpoint.USEast1));
}



var app = builder.Build();

app.MapGet("/", () => "Hello, world!");

var v1 = app.MapGroup("/v1");
v1.MapEndpoints();

Console.WriteLine("Starting app...");

app.Run();