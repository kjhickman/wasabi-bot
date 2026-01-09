namespace WasabiBot.Api.Features.Reminders.Abstractions;

public interface ITimeParsingService
{
    Task<DateTimeOffset?> ParseTimeAsync(string timeInput);
}
