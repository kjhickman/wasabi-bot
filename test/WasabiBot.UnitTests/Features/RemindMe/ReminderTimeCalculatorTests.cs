using WasabiBot.Api.Features.RemindMe.Services;

namespace WasabiBot.UnitTests.Features.RemindMe;

public class ReminderTimeCalculatorTests
{
    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private static ReminderTimeCalculator Create(DateTimeOffset utcNow) => new(new FixedTimeProvider(utcNow));

    [Test]
    public async Task ComputeRelativeUtc_AllZeros_ReturnsNull()
    {
        var baseTime = new DateTimeOffset(2025, 10, 10, 12, 30, 0, TimeSpan.Zero);
        var resolver = Create(baseTime);
        await Assert.That(resolver.ComputeRelativeUtc()).IsNull();
    }

    [Test]
    public async Task ComputeRelativeUtc_AnyNegative_ReturnsNull()
    {
        var resolver = Create(DateTimeOffset.UtcNow);
        await Assert.That(resolver.ComputeRelativeUtc(days: -1)).IsNull();
    }

    [Test]
    public async Task ComputeRelativeUtc_ValidComponents_AddsCorrectly_AndSecondsZero()
    {
        var baseTime = new DateTimeOffset(2025, 01, 15, 08, 00, 42, TimeSpan.Zero); // include non-zero seconds to ensure truncation
        var resolver = Create(baseTime);
        var result = resolver.ComputeRelativeUtc(months: 1, weeks: 2, days: 3, hours: 4, minutes: 5);
        await Assert.That(result).IsNotNull();
        var truncatedBase = new DateTimeOffset(baseTime.Year, baseTime.Month, baseTime.Day, baseTime.Hour, baseTime.Minute, 0, TimeSpan.Zero);
        var expected = truncatedBase.AddMonths(1).AddDays(2 * 7 + 3).AddHours(4).AddMinutes(5);
        await Assert.That(result!.Value).IsEqualTo(expected);
        await Assert.That(result.Value.Second).IsEqualTo(0);
    }

    [Test]
    public async Task ComputeRelativeUtc_TruncatesSecondsRegardlessOfOffsets()
    {
        var baseTime = new DateTimeOffset(2025, 02, 01, 10, 59, 59, TimeSpan.Zero);
        var resolver = Create(baseTime);
        var result = resolver.ComputeRelativeUtc(minutes: 1); // rolls into next hour/minute but should have zero seconds
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Second).IsEqualTo(0);
    }

    [Test]
    public async Task ComputeAbsoluteUtc_InvalidBounds_ReturnsNull()
    {
        var resolver = Create(DateTimeOffset.UtcNow);
        await Assert.That(resolver.ComputeAbsoluteUtc(13, 1)).IsNull();
        await Assert.That(resolver.ComputeAbsoluteUtc(0, 1)).IsNull();
        await Assert.That(resolver.ComputeAbsoluteUtc(1, 0)).IsNull();
        await Assert.That(resolver.ComputeAbsoluteUtc(1, 1, hour: 24)).IsNull();
        await Assert.That(resolver.ComputeAbsoluteUtc(1, 1, minute: 60)).IsNull();
    }

    [Test]
    public async Task ComputeAbsoluteUtc_InferYear_AndTime()
    {
        var baseTime = new DateTimeOffset(2025, 10, 10, 12, 34, 56, TimeSpan.Zero);
        var resolver = Create(baseTime);
        var result = resolver.ComputeAbsoluteUtc(month: 12, day: 25); // year=0 hour=-1 minute=-1 -> infer
        await Assert.That(result).IsNotNull();
        var r = result!.Value;
        await Assert.That(r.Year is 2025 or 2026).IsTrue();
    }

    [Test]
    public async Task ComputeAbsoluteUtc_RollForwardYearWhenPast()
    {
        var baseTime = new DateTimeOffset(2025, 10, 10, 12, 00, 00, TimeSpan.Zero);
        var resolver = Create(baseTime);
        var result = resolver.ComputeAbsoluteUtc(month: 1, day: 5); // January 5 already passed => roll forward
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Year).IsEqualTo(2026);
    }

    [Test]
    public async Task ComputeAbsoluteUtc_InvalidYear_ReturnsNull()
    {
        var baseTime = new DateTimeOffset(2025, 10, 10, 0, 0, 0, TimeSpan.Zero);
        var resolver = Create(baseTime);
        await Assert.That(resolver.ComputeAbsoluteUtc(month: 1, day: 1, year: 2024)).IsNull(); // past year
        await Assert.That(resolver.ComputeAbsoluteUtc(month: 1, day: 1, year: 2500)).IsNull(); // > 2200
    }
}
