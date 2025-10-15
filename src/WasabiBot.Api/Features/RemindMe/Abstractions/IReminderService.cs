using WasabiBot.Api.Features.RemindMe.Contracts;

namespace WasabiBot.Api.Features.RemindMe.Abstractions;

public interface IReminderService
{
    Task<bool> ScheduleAsync(CreateReminderRequest request);
}
