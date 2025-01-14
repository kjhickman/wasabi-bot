using Discord;
using Discord.Interactions;
using Discord.Rest;
using WasabiBot.Discord;

var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
if (token is null)
{
    throw new Exception("DISCORD_TOKEN environment variable is not set.");
}

var testGuildId = Environment.GetEnvironmentVariable("DISCORD_TEST_GUILD_ID");
if (testGuildId is null)
{
    throw new Exception("DISCORD_TEST_GUILD_ID environment variable is not set.");
}

var discord = new DiscordRestClient();
await discord.LoginAsync(TokenType.Bot, token);
        
// Add commands to the interaction service
var interactions = new InteractionService(discord);
await interactions.AddWasabiBotModules(null);
        
// Register commands to the test guild if in development
if (!string.IsNullOrWhiteSpace(testGuildId))
{
    var parsedId = ulong.Parse(testGuildId);
    await interactions.RegisterCommandsToGuildAsync(parsedId);
}
else
{
    await interactions.RegisterCommandsGloballyAsync();
}