using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WasabiBot.Api.Features.Stats;
using WasabiBot.UnitTests.Infrastructure.Discord;

namespace WasabiBot.UnitTests.Features.Stats;

public class StatsCommandTests
{
    private static StatsCommand CreateCommand(IStatsService statsService)
    {
        return new StatsCommand(
            NullLogger<StatsCommand>.Instance,
            statsService);
    }

    [Test]
    public async Task ExecuteAsync_RespondsWithStatsMessage()
    {
        var statsData = new StatsData
        {
            TotalInteractions = 100,
            ChannelInteractions = 20,
            MostUsedCommand = "reminder",
            MostUsedCommandCount = 45,
            TopUserName = "TopUser",
            TopUserId = 12345,
            TopUserCount = 30
        };

        var statsService = Substitute.For<IStatsService>();
        statsService
            .GetStatsAsync(Arg.Any<long>(), Arg.Any<long>())
            .Returns(statsData);

        var command = CreateCommand(statsService);
        var context = new FakeCommandContext(userId: 42, channelId: 1001, userDisplayName: "TestUser");

        await command.ExecuteAsync(context);

        var ephemerals = context.EphemeralMessages;
        await Assert.That(ephemerals.Count).IsEqualTo(1);
        await Assert.That(ephemerals.Single()).IsNotEmpty();
    }

    [Test]
    public async Task ExecuteAsync_CallsGetStatsWithCorrectChannelId()
    {
        var statsData = new StatsData
        {
            TotalInteractions = 100,
            ChannelInteractions = 20,
            MostUsedCommand = "reminder",
            MostUsedCommandCount = 45,
            TopUserName = "TopUser",
            TopUserId = 12345,
            TopUserCount = 30
        };

        var statsService = Substitute.For<IStatsService>();
        statsService
            .GetStatsAsync(Arg.Any<long>(), Arg.Any<long>())
            .Returns(statsData);

        var command = CreateCommand(statsService);
        var context = new FakeCommandContext(userId: 42, channelId: 9001, userDisplayName: "TestUser");

        await command.ExecuteAsync(context);

        await statsService.Received(1).GetStatsAsync(9001L, 3L);
    }

    [Test]
    public async Task ExecuteAsync_ResponseIncludesStatsHeader()
    {
        var statsData = new StatsData
        {
            TotalInteractions = 100,
            ChannelInteractions = 20,
            MostUsedCommand = "reminder",
            MostUsedCommandCount = 45,
            TopUserName = "TopUser",
            TopUserId = 12345,
            TopUserCount = 30
        };

        var statsService = Substitute.For<IStatsService>();
        statsService
            .GetStatsAsync(Arg.Any<long>(), Arg.Any<long>())
            .Returns(statsData);

        var command = CreateCommand(statsService);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context);

        var message = context.EphemeralMessages.Single();
        await Assert.That(message).Contains("📊 Bot Statistics");
    }

    [Test]
    public async Task ExecuteAsync_ResponseIncludesTotalInteractions()
    {
        var statsData = new StatsData
        {
            TotalInteractions = 12345,
            ChannelInteractions = 20,
            MostUsedCommand = "reminder",
            MostUsedCommandCount = 45,
            TopUserName = "TopUser",
            TopUserId = 12345,
            TopUserCount = 30
        };

        var statsService = Substitute.For<IStatsService>();
        statsService
            .GetStatsAsync(Arg.Any<long>(), Arg.Any<long>())
            .Returns(statsData);

        var command = CreateCommand(statsService);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context);

        var message = context.EphemeralMessages.Single();
        await Assert.That(message).Contains("Total interactions:");
        await Assert.That(message).Contains("12,345");
    }

    [Test]
    public async Task ExecuteAsync_ResponseIncludesChannelInteractions()
    {
        var statsData = new StatsData
        {
            TotalInteractions = 100,
            ChannelInteractions = 42,
            MostUsedCommand = "reminder",
            MostUsedCommandCount = 45,
            TopUserName = "TopUser",
            TopUserId = 12345,
            TopUserCount = 30
        };

        var statsService = Substitute.For<IStatsService>();
        statsService
            .GetStatsAsync(Arg.Any<long>(), Arg.Any<long>())
            .Returns(statsData);

        var command = CreateCommand(statsService);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context);

        var message = context.EphemeralMessages.Single();
        await Assert.That(message).Contains("This channel:");
        await Assert.That(message).Contains("42");
    }

    [Test]
    public async Task ExecuteAsync_ResponseIncludesMostUsedCommand()
    {
        var statsData = new StatsData
        {
            TotalInteractions = 100,
            ChannelInteractions = 20,
            MostUsedCommand = "caption",
            MostUsedCommandCount = 67,
            TopUserName = "TopUser",
            TopUserId = 12345,
            TopUserCount = 30
        };

        var statsService = Substitute.For<IStatsService>();
        statsService
            .GetStatsAsync(Arg.Any<long>(), Arg.Any<long>())
            .Returns(statsData);

        var command = CreateCommand(statsService);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context);

        var message = context.EphemeralMessages.Single();
        await Assert.That(message).Contains("Most Used Command");
        await Assert.That(message).Contains("/caption");
        await Assert.That(message).Contains("67");
    }

    [Test]
    public async Task ExecuteAsync_WhenMostUsedCommandIsEmpty_OmitsMostUsedCommandSection()
    {
        var statsData = new StatsData
        {
            TotalInteractions = 100,
            ChannelInteractions = 20,
            MostUsedCommand = null,
            MostUsedCommandCount = 0,
            TopUserName = "TopUser",
            TopUserId = 12345,
            TopUserCount = 30
        };

        var statsService = Substitute.For<IStatsService>();
        statsService
            .GetStatsAsync(Arg.Any<long>(), Arg.Any<long>())
            .Returns(statsData);

        var command = CreateCommand(statsService);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context);

        var message = context.EphemeralMessages.Single();
        await Assert.That(!message.Contains("Most Used Command")).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_ResponseIncludesTopUser()
    {
        var statsData = new StatsData
        {
            TotalInteractions = 100,
            ChannelInteractions = 20,
            MostUsedCommand = "reminder",
            MostUsedCommandCount = 45,
            TopUserName = "PowerUser",
            TopUserId = 12345,
            TopUserCount = 89
        };

        var statsService = Substitute.For<IStatsService>();
        statsService
            .GetStatsAsync(Arg.Any<long>(), Arg.Any<long>())
            .Returns(statsData);

        var command = CreateCommand(statsService);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context);

        var message = context.EphemeralMessages.Single();
        await Assert.That(message).Contains("Top User");
        await Assert.That(message).Contains("PowerUser");
        await Assert.That(message).Contains("89");
    }

    [Test]
    public async Task ExecuteAsync_WhenTopUserNameIsEmpty_OmitsTopUserSection()
    {
        var statsData = new StatsData
        {
            TotalInteractions = 100,
            ChannelInteractions = 20,
            MostUsedCommand = "reminder",
            MostUsedCommandCount = 45,
            TopUserName = null,
            TopUserId = 0,
            TopUserCount = 0
        };

        var statsService = Substitute.For<IStatsService>();
        statsService
            .GetStatsAsync(Arg.Any<long>(), Arg.Any<long>())
            .Returns(statsData);

        var command = CreateCommand(statsService);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context);

        var message = context.EphemeralMessages.Single();
        await Assert.That(!message.Contains("Top User")).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_ResponseIsEphemeral()
    {
        var statsData = new StatsData
        {
            TotalInteractions = 100,
            ChannelInteractions = 20,
            MostUsedCommand = "reminder",
            MostUsedCommandCount = 45,
            TopUserName = "TopUser",
            TopUserId = 12345,
            TopUserCount = 30
        };

        var statsService = Substitute.For<IStatsService>();
        statsService
            .GetStatsAsync(Arg.Any<long>(), Arg.Any<long>())
            .Returns(statsData);

        var command = CreateCommand(statsService);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context);

        var (_, ephemeral) = context.Messages.Single();
        await Assert.That(ephemeral).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_WhenBothOptionalSectionsEmpty_ShowsOnlyRequiredSections()
    {
        var statsData = new StatsData
        {
            TotalInteractions = 100,
            ChannelInteractions = 20,
            MostUsedCommand = null,
            MostUsedCommandCount = 0,
            TopUserName = null,
            TopUserId = 0,
            TopUserCount = 0
        };

        var statsService = Substitute.For<IStatsService>();
        statsService
            .GetStatsAsync(Arg.Any<long>(), Arg.Any<long>())
            .Returns(statsData);

        var command = CreateCommand(statsService);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context);

        var message = context.EphemeralMessages.Single();
        await Assert.That(message).Contains("Interaction Counts");
        await Assert.That(!message.Contains("Most Used Command")).IsTrue();
        await Assert.That(!message.Contains("Top User")).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_FormatsNumbersWithThousandsSeparators()
    {
        var statsData = new StatsData
        {
            TotalInteractions = 1000000,
            ChannelInteractions = 500000,
            MostUsedCommand = "test",
            MostUsedCommandCount = 250000,
            TopUserName = "TopUser",
            TopUserId = 12345,
            TopUserCount = 100000
        };

        var statsService = Substitute.For<IStatsService>();
        statsService
            .GetStatsAsync(Arg.Any<long>(), Arg.Any<long>())
            .Returns(statsData);

        var command = CreateCommand(statsService);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context);

        var message = context.EphemeralMessages.Single();
        // Check for formatted numbers with commas
        await Assert.That(message).Contains("1,000,000");
        await Assert.That(message).Contains("500,000");
    }

    [Test]
    public async Task ExecuteAsync_IncludesInteractionCountsHeader()
    {
        var statsData = new StatsData
        {
            TotalInteractions = 100,
            ChannelInteractions = 20,
            MostUsedCommand = "reminder",
            MostUsedCommandCount = 45,
            TopUserName = "TopUser",
            TopUserId = 12345,
            TopUserCount = 30
        };

        var statsService = Substitute.For<IStatsService>();
        statsService
            .GetStatsAsync(Arg.Any<long>(), Arg.Any<long>())
            .Returns(statsData);

        var command = CreateCommand(statsService);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context);

        var message = context.EphemeralMessages.Single();
        await Assert.That(message).Contains("## Interaction Counts");
    }
}
