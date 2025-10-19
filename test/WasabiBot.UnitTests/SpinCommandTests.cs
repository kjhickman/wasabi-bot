using WasabiBot.Api.Features.Spin;

namespace WasabiBot.UnitTests;

public class SpinCommandTests
{
    private sealed class DeterministicRandom : Random
    {
        private readonly int _fixed;
        public DeterministicRandom(int fixedValue) => _fixed = fixedValue;
        public override int Next(int maxValue) => _fixed % maxValue;
    }

    [Test]
    public async Task ChooseOption_WithinBounds_ReturnsIndex()
    {
        var options = new[] { "jackbox", "valorant", "rocket league" };
        var chosen = SpinCommand.ChooseOption(options, new DeterministicRandom(1));
        await Assert.That(chosen).IsEqualTo("valorant");
    }

    [Test]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(7)]
    public async Task ChooseOption_ValidCounts_NoException(int count)
    {
        var options = Enumerable.Range(0, count).Select(i => $"opt{i}").ToArray();
        var chosen = SpinCommand.ChooseOption(options, new DeterministicRandom(0));
        await Assert.That(options.Contains(chosen)).IsTrue();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(8)]
    [Arguments(9)]
    public async Task ChooseOption_InvalidCounts_Throws(int count)
    {
        var options = Enumerable.Range(0, count).Select(i => $"opt{i}").ToArray();
        await Assert.That(() => SpinCommand.ChooseOption(options)).Throws<ArgumentException>();
    }
}
