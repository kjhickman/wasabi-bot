using System.Net.Http.Json;
using WasabiBot.Web;
using WasabiBot.Web.Commands;

// Get arguments
var applicationId = args[0];
var token = args[1];

// Configure HttpClient
var http = new HttpClient();
http.DefaultRequestHeaders.Add("Authorization", $"Bot {token}");

// Register global commands
var url = $"https://discord.com/api/v10/applications/{applicationId}/commands";
await http.PutAsJsonAsync(url, Commands.Definitions, WebJsonContext.Default.ApplicationCommandArray);
