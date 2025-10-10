using WasabiBot.Api.Features.RemindMe.Services;
using Xunit;

namespace WasabiBot.UnitTests;

public class ReminderTimeCalculatorTests
{
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private static ReminderTimeCalculator Create(DateTimeOffset utcNow) => new(new FixedTimeProvider(utcNow));

    [Fact]
    public void ComputeRelativeUtc_AllZeros_ReturnsNull()
    {
        var baseTime = new DateTimeOffset(2025, 10, 10, 12, 30, 0, TimeSpan.Zero);
        var resolver = Create(baseTime);
        Assert.Null(resolver.ComputeRelativeUtc());
    }

    [Fact]
    public void ComputeRelativeUtc_AnyNegative_ReturnsNull()
    {
        var resolver = Create(DateTimeOffset.UtcNow);
        Assert.Null(resolver.ComputeRelativeUtc(days: -1));
    }

    [Fact]
    public void ComputeRelativeUtc_ValidComponents_AddsCorrectly()
    {
        var baseTime = new DateTimeOffset(2025, 01, 15, 08, 00, 00, TimeSpan.Zero);
        var resolver = Create(baseTime);
        var result = resolver.ComputeRelativeUtc(months: 1, weeks: 2, days: 3, hours: 4, minutes: 5, seconds: 6);
        Assert.NotNull(result);
        var expected = baseTime.AddMonths(1).AddDays(2 * 7 + 3).AddHours(4).AddMinutes(5).AddSeconds(6);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void ComputeAbsoluteUtc_InvalidBounds_ReturnsNull()
    {
        var resolver = Create(DateTimeOffset.UtcNow);
        Assert.Null(resolver.ComputeAbsoluteUtc(13, 1));
        Assert.Null(resolver.ComputeAbsoluteUtc(0, 1));
        Assert.Null(resolver.ComputeAbsoluteUtc(1, 0));
        Assert.Null(resolver.ComputeAbsoluteUtc(1, 1, hour: 24));
        Assert.Null(resolver.ComputeAbsoluteUtc(1, 1, minute: 60));
    }

    [Fact]
    public void ComputeAbsoluteUtc_InferYear_AndTime()
    {
        var baseTime = new DateTimeOffset(2025, 10, 10, 12, 34, 56, TimeSpan.Zero);
        var resolver = Create(baseTime);
        var result = resolver.ComputeAbsoluteUtc(month: 12, day: 25); // year=0 hour=-1 minute=-1 -> infer
        Assert.NotNull(result);
        var r = result.Value;
        Assert.True(r.Year is 2025 or 2026);
    }

    [Fact]
    public void ComputeAbsoluteUtc_RollForwardYearWhenPast()
    {
        var baseTime = new DateTimeOffset(2025, 10, 10, 12, 00, 00, TimeSpan.Zero);
        var resolver = Create(baseTime);
        var result = resolver.ComputeAbsoluteUtc(month: 1, day: 5); // January 5 already passed => roll forward
        Assert.NotNull(result);
        Assert.Equal(2026, result.Value.Year);
    }

    [Fact]
    public void ComputeAbsoluteUtc_InvalidYear_ReturnsNull()
    {
        var baseTime = new DateTimeOffset(2025, 10, 10, 0, 0, 0, TimeSpan.Zero);
        var resolver = Create(baseTime);
        Assert.Null(resolver.ComputeAbsoluteUtc(month: 1, day: 1, year: 2024)); // past year
        Assert.Null(resolver.ComputeAbsoluteUtc(month: 1, day: 1, year: 2500)); // > 2200
    }
}

