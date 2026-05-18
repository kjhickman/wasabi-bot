namespace WasabiBot.Api.Features.Reminders.Abstractions;

public sealed class TimeParsingException : Exception
{
    public TimeParsingException(string message) : base(message)
    {
    }
}
