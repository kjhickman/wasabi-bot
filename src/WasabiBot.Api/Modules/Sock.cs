using System.ComponentModel;
using Microsoft.Extensions.AI;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace WasabiBot.Api.Modules;

public class Sock
{
    private static readonly ChatOptions? ChatOptions;

    static Sock()
    {
        var sockFunction = AIFunctionFactory.Create(CalculateSockPrice);
        ChatOptions = new ChatOptions { Tools = [sockFunction] };
    }

    public static async Task<string> Command(IChatClient chat, ApplicationCommandContext ctx, string question)
    {
        var prompt = """
                     You are a sock salesman. You have the personality of Michael Scott from The Office.
                     Respond in a funny way but only 1 sentence long. You can calculate the price of socks using the CalculateSockPrice function.
                     """;
        var response = await chat.GetResponseAsync(prompt, ChatOptions);
        return response.Text;
    }

    [Description("Calculates the price of socks.")]
    private static decimal CalculateSockPrice([Description("The number of socks to purchase")]int count)
    {
        return 0.99m * count;
    }
}
