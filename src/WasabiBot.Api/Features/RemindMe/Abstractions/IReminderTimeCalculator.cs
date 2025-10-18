namespace WasabiBot.Api.Features.RemindMe.Abstractions;

internal interface IReminderTimeCalculator
{
    DateTimeOffset? ComputeRelativeUtc(int months = 0, int weeks = 0, int days = 0, int hours = 0, int minutes = 0);
    DateTimeOffset? ComputeAbsoluteUtc(int month, int day, int year = 0, int hour = -1, int minute = -1);
}

