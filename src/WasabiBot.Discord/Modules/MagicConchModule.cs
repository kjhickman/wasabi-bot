using System.Text;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace WasabiBot.Discord.Modules;

public class MagicConchModule : RestInteractionModuleBase<RestInteractionContext>
{
    private readonly Tracer _tracer;
    private readonly ILogger<MagicConchModule> _logger;

    public MagicConchModule(Tracer tracer, ILogger<MagicConchModule> logger)
    {
        _tracer = tracer;
        _logger = logger;
    }

    // TODO: I would like to have each command in its own class / file with its own dependencies. Preferably
    //       more like minimal apis as opposed to controllers
    [SlashCommand("conch", "Ask the Magic Conch!")]
    public async Task MagicConch(string question)
    {
        using var span = _tracer.StartActiveSpan($"{nameof(MagicConchModule)}.{nameof(MagicConch)}");
        _logger.LogInformation("Processing Magic Conch question from {User}", Context.User);
        var response = BuildMagicConchResponse(question, Context.User.Mention);
        await RespondAsync(response);
    }
    
    internal static string BuildMagicConchResponse(string question, string mention)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.Append($"{mention} asked {Format.Italics(question)}");
        stringBuilder.AppendLine();
        var response = RandomlyChooseResponse();
        stringBuilder.Append($"The Magic Conch says... {response}");
        return stringBuilder.ToString();
    }
    
    private static readonly List<(string Response, int Weight)> MagicConchResponses =
    [
        ("Yes", 44),
        ("No", 32),
        ("I don't think so", 12),
        ("Maybe some day", 9),
        ("Try asking again", 3),
    ];

    private static string RandomlyChooseResponse()
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