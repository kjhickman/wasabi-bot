using System.Text;
using WasabiBot.Core.Discord;
using WasabiBot.Core.Interfaces;

namespace WasabiBot.DataAccess.Commands.Handlers;

public class WheelCommand : ISyncCommand
{
    public static string Name => "spin";

    public InteractionResponse Execute(Interaction interaction)
    {
        var options = interaction.Data?.Options;
        if (options is null || options.Length == 0)
        {
            return InteractionResponse.Reply("No wheel options provided");
        }
        
        var selection = options[Random.Shared.Next(options.Length)].Value?.GetString();
        if (selection is null)
        {
            return InteractionResponse.Reply("Something went wrong");
        }
        
        return InteractionResponse.Reply(BuildResponse(options, selection));
    }
    
    private string BuildResponse(InteractionDataOption[] options, string selection)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.Append($"Spinning a wheel with options: {string.Join(", ", options.Select(x => x.Value?.GetString()))}");
        stringBuilder.AppendLine();
        stringBuilder.Append($"The wheel lands on... {selection}");
        return stringBuilder.ToString();
    }
}