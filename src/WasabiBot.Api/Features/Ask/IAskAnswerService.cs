namespace WasabiBot.Api.Features.Ask;

internal interface IAskAnswerService
{
    Task<AskAnswer> AnswerAsync(string question, CancellationToken cancellationToken = default);
}
