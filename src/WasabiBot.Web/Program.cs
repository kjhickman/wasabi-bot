using System.Data;
using Amazon;
using Amazon.Runtime;
using Amazon.RuntimeDependencies;
using Amazon.SecurityToken;
using Amazon.SQS;
using Amazon.SQS.Model;
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
    try 
    {
        var stsConfig = new AmazonSecurityTokenServiceConfig
        {
            RegionEndpoint = RegionEndpoint.USEast1
        };

        Console.WriteLine("Starting AWS credential setup...");
    
        var webIdentityCredentials = new AssumeRoleWithWebIdentityCredentials(
            roleArn: Environment.GetEnvironmentVariable("AWS_ROLE_ARN")!,
            webIdentityTokenFile: Environment.GetEnvironmentVariable("AWS_WEB_IDENTITY_TOKEN_FILE")!,
            roleSessionName: Environment.GetEnvironmentVariable("AWS_ROLE_SESSION_NAME")!
        );

        Console.WriteLine("Created web identity credentials");
    
        // Try to get credentials explicitly to test the setup
        try 
        {
            var credentials = await webIdentityCredentials.GetCredentialsAsync();
            Console.WriteLine("Successfully retrieved credentials from STS");
        }
        catch (Exception credEx)
        {
            Console.WriteLine($"Error getting credentials: {credEx}");
        }

        Console.WriteLine("Registering STS client...");
        GlobalRuntimeDependencyRegistry.Instance.RegisterSecurityTokenServiceClient(
            new AmazonSecurityTokenServiceClient(webIdentityCredentials, stsConfig)
        );
        Console.WriteLine("STS client registered");

        Console.WriteLine("Creating SQS client...");
        var sqsClient = new AmazonSQSClient(webIdentityCredentials, RegionEndpoint.USEast1);
    
        // Test SQS access
        try 
        {
            var queueUrls = await sqsClient.ListQueuesAsync(new ListQueuesRequest());
            Console.WriteLine($"Successfully listed queues: {queueUrls.QueueUrls.Count} found");
        }
        catch (Exception sqsEx)
        {
            Console.WriteLine($"Error accessing SQS: {sqsEx}");
        }

        builder.Services.AddSingleton<IAmazonSQS>(sqsClient);
        Console.WriteLine("SQS client registered");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Overall setup error: {ex}");
    }
}



var app = builder.Build();

app.MapGet("/", () => "Hello, world!");

var v1 = app.MapGroup("/v1");
v1.MapEndpoints();

Console.WriteLine("Starting app...");

app.Run();