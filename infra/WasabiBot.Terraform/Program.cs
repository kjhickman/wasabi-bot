using HashiCorp.Cdktf;
using WasabiBot.Terraform;

var app = new App();
var environment = args[0];
new WasabiBotStack(app, "WasabiBot.Terraform", environment);
app.Synth();
