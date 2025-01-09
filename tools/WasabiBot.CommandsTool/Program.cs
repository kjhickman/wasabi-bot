using Discord;
using Discord.Interactions;
using Discord.Rest;
using WasabiBot.Discord;

var token = args[0];

var discordClient = new DiscordRestClient();
await discordClient.LoginAsync(TokenType.Bot, token);

var interactionService = new InteractionService(discordClient);
await interactionService.AddWasabiBotModules();

Console.WriteLine("Success!");
