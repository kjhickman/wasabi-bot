using WasabiBot.Core.Discord;
using WasabiBot.Core.Interfaces;

namespace WasabiBot.Web.Commands.Handlers;

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
        return InteractionResponse.Reply(GetResponse());
    }

    private string GetResponse()
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