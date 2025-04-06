using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using NetCord.Services.ApplicationCommands;

namespace WasabiBot.Api.Modules;

public static class MagicConch
{
    private static readonly ChatOptions? ChatOptions;

    static MagicConch()
    {
        var rngTool = AIFunctionFactory.Create(RandomlyChooseResponse);
        ChatOptions = new ChatOptions { Tools = [rngTool] };
    }

    public static async Task<string> Command(IChatClient chat, ApplicationCommandContext ctx, string question)
    {
        var prompt = "The magic conch shell is a mystical object that can answer any question. " +
            "The user asks a yes/no question, and the magic conch shell provides a response. " +
            "If the question is not a yes/no question, respond with 'Try asking again'" +
            "If you know the answer, you may only respond with one of the following words or phrases: Yes, No. " +
            "If you don't know the answer, use RandomlyChooseResponse()" +
            $"The user asked: {question}";
        var response = await chat.GetResponseAsync(prompt, ChatOptions);
        return response.Text;
    }

    [Description("Randomly chooses a response from the magic conch shell if the answer is unknown.")]
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

        throw new UnreachableException("Failed to randomly choose a response.");
    }

    private static readonly List<(string Response, int Weight)> MagicConchResponses =
    [
        ("Yes", 44),
        ("No", 32),
        ("I don't think so", 12),
        ("Maybe", 9),
        ("Try asking again", 3),
    ];
}
