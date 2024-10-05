using dotenv.net;
using HashiCorp.Cdktf;
using Microsoft.Extensions.Configuration;
using WasabiBot.Terraform;
using WasabiBot.Terraform.Settings;

var app = new App();

DotEnv.Load();
var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();
var environmentVariables = new EnvironmentVariables();
configuration.Bind(environmentVariables); // TODO: validate these

new WasabiBotStack(app, "WasabiBot.Terraform", environmentVariables);

app.Synth();
