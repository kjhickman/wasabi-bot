using Amazon;
using Amazon.DSQL.Util;
using Amazon.Runtime.Credentials;
using Microsoft.Extensions.Configuration;
using WasabiBot.Migrations;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

// Prefer explicit connection string if provided (likely from Aspire).
var connectionString = configuration.GetConnectionString("wasabi-db");

if (string.IsNullOrWhiteSpace(connectionString))
{
    const string database = "postgres";
    const string username = "admin";
    const string dsqlEndpoint = "dvthki5gk26y76agneiy6wcn64.dsql.us-east-1.on.aws";

    var creds = DefaultAWSCredentialsIdentityResolver.GetCredentials();
    var token = DSQLAuthTokenGenerator.GenerateDbConnectAdminAuthToken(creds, RegionEndpoint.USEast1, dsqlEndpoint);

    connectionString = $"Host={dsqlEndpoint};Database={database};Username={username};Password={token};Ssl Mode=Require;NoResetOnClose=True";
    Console.WriteLine("Using Aurora DSQL token-based connection for migrations.");
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'wasabi-db' not found and DSQL env vars not set.");
}

MigrationRunner.Run(connectionString);




