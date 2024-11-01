using System.ComponentModel.DataAnnotations;
using dotenv.net;
using HashiCorp.Cdktf;
using Microsoft.Extensions.Configuration;
using WasabiBot.Terraform;
using WasabiBot.Terraform.Settings;
using WasabiBot.Terraform.Stacks;

var app = new App();

DotEnv.Load(new DotEnvOptions(envFilePaths: ["../../.env", "./.env"]));
var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var environmentVariables = new EnvironmentVariables();
configuration.Bind(environmentVariables);

var validationContext = new ValidationContext(environmentVariables);
var validationResults = new List<ValidationResult>();
var isValid = Validator.TryValidateObject(environmentVariables, validationContext, validationResults);
if (!isValid)
{
    foreach (var validationResult in validationResults)
    {
        Console.WriteLine(validationResult.ErrorMessage);
    }
    return;
}

var foo = new WasabiBotSharedStack(app, "WasabiBotShared.Terraform");
Console.WriteLine(foo.EcrRepositoryUrl);
new WasabiBotStack(app, "WasabiBot.Terraform", environmentVariables);

app.Synth();
