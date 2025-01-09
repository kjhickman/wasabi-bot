using System.Text;
using Discord;
using Discord.Interactions;
using Discord.Rest;

namespace WasabiBot.Discord.Modules;

public class MagicConchModule : RestInteractionModuleBase<RestInteractionContext>
{
    // TODO: I would like to have each command in it's own class / file with it's own dependencies. Preferably
    //       more like minimal apis as opposed to controllers
    [SlashCommand("conch", "Ask the Magic Conch!")]
    public async Task MagicConch(string question)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.Append($"{Context.User.Mention} asked {Format.Italics(question)}");
        stringBuilder.AppendLine();
        var response = GetMagicConchResponse();
        stringBuilder.Append($"The Magic Conch says... {response}");
        await RespondAsync(stringBuilder.ToString());
    }
    
    private static readonly List<(string Response, int Weight)> MagicConchResponses =
    [
        ("Yes", 44),
        ("No", 32),
        ("I don't think so", 12),
        ("Maybe some day", 9),
        ("Try asking again", 3),
    ];

    private static string GetMagicConchResponse()
    {
        var totalWeight = MagicConchResponses.Sum(r => r.Weight);
        var randomNumber = Random.Shared.Next(totalWeight);
        
        var currentWeight = 0;
        foreach (var response in MagicConchResponses)
        {
            currentWeight += response.Weight;
            if (randomNumber < currentWeight)
                return response.Response;
        }

        // Fallback, should be unreachable
        return MagicConchResponses[0].Response;
    }
}