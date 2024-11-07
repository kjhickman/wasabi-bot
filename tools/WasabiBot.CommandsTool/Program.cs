using System.Net.Http.Json;
using WasabiBot.Core;
using WasabiBot.DataAccess.Commands;

// Get arguments
var applicationId = "1103853199322001468";
var token = "***REMOVED***";

// Configure HttpClient
var http = new HttpClient();
http.DefaultRequestHeaders.Add("Authorization", $"Bot {token}");

// Register global commands
var url = $"https://discord.com/api/v10/applications/{applicationId}/commands";
Console.WriteLine($"Registering global commands for ApplicationId: {applicationId}");
var responseMessage = await http.PutAsJsonAsync(url, Commands.Definitions, CoreJsonContext.Default.ApplicationCommandArray);
var content = await responseMessage.Content.ReadAsStringAsync();
responseMessage.EnsureSuccessStatusCode();
Console.WriteLine("Success!");
