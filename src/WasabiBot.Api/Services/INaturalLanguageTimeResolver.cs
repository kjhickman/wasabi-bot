namespace WasabiBot.Api.Services;

/// <summary>
/// Resolves natural-language-derived temporal components into UTC DateTimeOffsets.
/// Provides both relative (additive) and absolute (calendar-based) calculations.
/// </summary>
internal interface INaturalLanguageTimeResolver
{
    /// <summary>
    /// Computes a future UTC timestamp by applying relative offsets to the current time.
    /// Returns null when the combination is invalid (negative values or all zero).
    /// </summary>
    DateTimeOffset? ComputeRelativeUtc(int months = 0, int weeks = 0, int days = 0, int hours = 0, int minutes = 0, int seconds = 0);

    /// <summary>
    /// Computes a future (or adjusted) UTC timestamp from calendar components.
    /// Returns null for invalid inputs. If year is 0, current year is assumed; if resulting time is past, a 1-year roll forward is applied.
    /// </summary>
    DateTimeOffset? ComputeAbsoluteUtc(int month, int day, int year = 0, int hour = -1, int minute = -1, string? timeZoneId = null);
}

