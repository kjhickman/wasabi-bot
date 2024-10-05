using dotenv.net;
using HashiCorp.Cdktf;
using Microsoft.Extensions.Configuration;
using VinceBot.Terraform;
using VinceBot.Terraform.Settings;

var app = new App();

DotEnv.Load();
var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();
var environmentVariables = new EnvironmentVariables();
configuration.Bind(environmentVariables); // TODO: validate these

new VinceBotStack(app, "VinceBot.Terraform", environmentVariables);

app.Synth();
