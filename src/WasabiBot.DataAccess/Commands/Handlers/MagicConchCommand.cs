using System.Text;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Interfaces;

namespace WasabiBot.DataAccess.Commands.Handlers;

public class MagicConchCommand: ISyncCommand
{
    public static string Name => "conch";
    
    private static readonly List<(string Response, int Weight)> Responses =
    [
        ("Yes", 44),
        ("No", 32),
        ("I don't think so", 12),
        ("Maybe some day", 9),
        ("Try asking again", 3),
    ];

    public InteractionResponse Execute(Interaction interaction)
    {
        var userId = interaction.User?.Id ?? interaction.GuildMember?.User?.Id;
        var mention = userId is null ? "User" : $"<@{userId}>";
        var question = interaction.Data?.Options?.FirstOrDefault()?.Value?.GetString();
        if (question is null)
        {
            return InteractionResponse.Reply(GetMagicConchResponse());
        }
        
        return InteractionResponse.Reply(BuildResponse(mention, question));
    }

    private string BuildResponse(string mention, string question)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.Append($"{mention} asked \"{question}\"");
        stringBuilder.AppendLine();
        var response = GetMagicConchResponse();
        stringBuilder.Append($"The Magic Conch says... {response}");
        return stringBuilder.ToString();
    }

    private string GetMagicConchResponse()
    {
        var totalWeight = Responses.Sum(r => r.Weight);
        var randomNumber = Random.Shared.Next(totalWeight);
        
        var currentWeight = 0;
        foreach (var response in Responses)
        {
            currentWeight += response.Weight;
            if (randomNumber < currentWeight)
                return response.Response;
        }

        // Fallback (shouldn't occur)
        return Responses[0].Response;
    }
}