using System.ComponentModel;
using System.Diagnostics;

namespace WasabiBot.Api.Features.MagicConch;

public class MagicConchTool : IMagicConchTool
{
    private readonly ILogger<MagicConchTool> _logger;
    private readonly Random _random;

    public MagicConchTool(ILogger<MagicConchTool> logger, Random random)
    {
        _logger = logger;
        _random = random;
    }

    [Description("Randomly chooses a response from the magic conch shell if the answer is unknown.")]
    public string GetMagicConchResponse(string question)
    {
        _logger.LogInformation("Invoked Magic Conch tool for question: {Question}", question);
        var randomNumber = _random.Next(TotalWeight);

        var currentWeight = 0;
        foreach (var response in MagicConchResponses)
        {
            currentWeight += response.Weight;
            if (randomNumber < currentWeight)
            {
                return response.Response;
            }
        }

        throw new UnreachableException("Failed to randomly choose a response.");
    }

    private static readonly (string Response, int Weight)[] MagicConchResponses =
    [
        ("Yes", 44),
        ("No", 32),
        ("I don't think so", 12),
        ("Maybe", 9),
        ("Try asking again", 3),
    ];

    private static readonly int TotalWeight = MagicConchResponses.Sum(static r => r.Weight);
}
