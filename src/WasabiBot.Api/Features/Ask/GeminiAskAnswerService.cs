using Google.GenAI;
using Google.GenAI.Types;

namespace WasabiBot.Api.Features.Ask;

internal sealed class GeminiAskAnswerService(Client client) : IAskAnswerService
{
    private const string Model = "gemini-3.5-flash";
    private const string SystemPrompt = "You answer one-off user questions in Discord. Keep the reply short and concise. There will be no follow-up conversation, so answer the question directly in a single response.";

    private readonly Client _client = client;

    public async Task<AskAnswer> AnswerAsync(string question, CancellationToken cancellationToken = default)
    {
        var response = await _client.Models.GenerateContentAsync(
            model: Model,
            contents: question,
            config: new GenerateContentConfig
            {
                SystemInstruction = new Content
                {
                    Parts = [new Part { Text = SystemPrompt }]
                },
                Tools = [new Tool { GoogleSearch = new GoogleSearch() }]
            },
            cancellationToken: cancellationToken);

        return new AskAnswer(response.Text ?? string.Empty);
    }
}
