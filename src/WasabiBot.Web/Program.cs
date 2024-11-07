using System.Data;
using Amazon;
using Amazon.Runtime;
using Amazon.RuntimeDependencies;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Amazon.SQS;
using Npgsql;
using WasabiBot.Web;
using WasabiBot.Core.Interfaces;
using WasabiBot.DataAccess.Commands;
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
    // Read the web identity token file path from env var
    var tokenFile = Environment.GetEnvironmentVariable("AWS_WEB_IDENTITY_TOKEN_FILE")!;
    var roleArn = Environment.GetEnvironmentVariable("AWS_ROLE_ARN");
    var session = Environment.GetEnvironmentVariable("AWS_ROLE_SESSION_NAME");
        
    // Read the token file
    var token = await File.ReadAllTextAsync(tokenFile);
        
    // Create assume role request
    var assumeRoleRequest = new AssumeRoleWithWebIdentityRequest
    {
        RoleArn = roleArn,
        WebIdentityToken = token,
        RoleSessionName = session
    };

    // Create STS client
    using var stsClient = new AmazonSecurityTokenServiceClient(RegionEndpoint.USEast1);
        
    // Get temporary credentials
    var response = await stsClient.AssumeRoleWithWebIdentityAsync(assumeRoleRequest);
        
    // Create credentials object
    var credentials = new SessionAWSCredentials(
        response.Credentials.AccessKeyId,
        response.Credentials.SecretAccessKey,
        response.Credentials.SessionToken
    );
    
    builder.Services.AddSingleton<IAmazonSQS>(new AmazonSQSClient(credentials, RegionEndpoint.USEast1));
}

var app = builder.Build();

app.MapGet("/", () => "Hello, world!");

var v1 = app.MapGroup("/v1");
v1.MapEndpoints();

Console.WriteLine("Starting app...");

app.Run();