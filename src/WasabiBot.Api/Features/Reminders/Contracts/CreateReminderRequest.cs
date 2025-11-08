namespace WasabiBot.Api.Features.Reminders.Contracts;

public class CreateReminderRequest
{
    public required long UserId { get; set; }
    public required long ChannelId { get; set; }
    public required string ReminderMessage { get; set; }
    public required DateTimeOffset RemindAt { get; set; }
}

