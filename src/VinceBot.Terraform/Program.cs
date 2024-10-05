using System;
using HashiCorp.Cdktf;
using VinceBot.Terraform;

// ReSharper disable ObjectCreationAsStatement

var app = new App();
new MainStack(app, "VinceBot.CDKTF");
app.Synth();
Console.WriteLine("App synth complete");