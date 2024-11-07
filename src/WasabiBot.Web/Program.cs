using System.Data;
using Amazon;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Amazon.SQS;
using Npgsql;
using WasabiBot.Core.Constants;
using WasabiBot.Web;
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

// Add AWS Services. TODO: Move to a separate extension methods DI/Aws.cs
if (!builder.Environment.IsDevelopment())
{
    var roleArn = Environment.GetEnvironmentVariable(Aws.RoleArn);
    var session = Environment.GetEnvironmentVariable(Aws.RoleSessionName);
    var tokenPath = Environment.GetEnvironmentVariable(Aws.WebIdentityTokenFile);
    var token = await File.ReadAllTextAsync(tokenPath!);
    
    var assumeRoleRequest = new AssumeRoleWithWebIdentityRequest
    {
        RoleArn = roleArn,
        WebIdentityToken = token,
        RoleSessionName = session
    };
    
    using var stsClient = new AmazonSecurityTokenServiceClient(RegionEndpoint.USEast1);
    var response = await stsClient.AssumeRoleWithWebIdentityAsync(assumeRoleRequest);
    var credentials = new SessionAWSCredentials(
        response.Credentials.AccessKeyId,
        response.Credentials.SecretAccessKey,
        response.Credentials.SessionToken
    );
    
    // Add AWS Services
    builder.Services.AddSingleton<IAmazonSQS>(new AmazonSQSClient(credentials, RegionEndpoint.USEast1));
}

var app = builder.Build();

app.MapGet("/", () => "Hello, world!");

var v1 = app.MapGroup("/v1");
v1.MapEndpoints();

Console.WriteLine("Starting app...");

app.Run();