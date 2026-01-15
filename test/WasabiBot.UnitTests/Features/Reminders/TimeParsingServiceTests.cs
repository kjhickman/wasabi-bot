using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.Reminders.Services;
using WasabiBot.Api.Infrastructure.AI;
using WasabiBot.UnitTests.Builders;

namespace WasabiBot.UnitTests.Features.Reminders;

public class TimeParsingServiceTests
{
    private static TimeParsingService CreateService(
        TimeProvider? timeProvider = null,
        IChatClient? chatClient = null)
    {
        timeProvider ??= TimeProvider.System;
        chatClient ??= Substitute.For<IChatClient>();
        var chatClientFactory = Substitute.For<IChatClientFactory>();
        chatClientFactory.GetChatClient(Arg.Any<LlmPreset>()).Returns(chatClient);
        var logger = NullLogger<TimeParsingService>.Instance;
        var tracer = TracerProvider.Default.GetTracer("timeparsing-tests");

        return new TimeParsingService(timeProvider, logger, chatClientFactory, tracer);
    }

    [Test]
    public async Task ParseTimeAsync_ThrowsWhenInputIsNull()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.ParseTimeAsync(null!));
    }

    [Test]
    public async Task ParseTimeAsync_ThrowsWhenInputIsWhitespace()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.ParseTimeAsync("   "));
    }

    [Test]
    public async Task ParseTimeAsync_CallsChatClientWithTimeInput()
    {
        var chatClient = Substitute.For<IChatClient>();
        var response = ChatResponseBuilder.Create()
            .WithAssistantText("2025-08-08T14:00:00Z")
            .Build();
        chatClient.GetResponseAsync(Arg.Any<IList<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var service = CreateService(chatClient: chatClient);

        await service.ParseTimeAsync("tomorrow at 2pm");

        await chatClient.Received(1).GetResponseAsync(
            Arg.Is<IList<ChatMessage>>(messages => 
                messages.Any(m => m.Role == ChatRole.User && m.Text == "tomorrow at 2pm")),
            Arg.Any<ChatOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ParseTimeAsync_IncludesSystemPromptWithCurrentTime()
    {
        var timeProvider = Substitute.For<TimeProvider>();
        var currentTime = new DateTimeOffset(2025, 1, 15, 14, 30, 0, TimeSpan.FromHours(-6));
        timeProvider.GetUtcNow().Returns(currentTime);

        var chatClient = Substitute.For<IChatClient>();
        var response = ChatResponseBuilder.Create()
            .WithAssistantText("2025-08-08T14:00:00Z")
            .Build();
        chatClient.GetResponseAsync(Arg.Any<IList<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var service = CreateService(timeProvider, chatClient);

        await service.ParseTimeAsync("in 2 hours");

        await chatClient.Received(1).GetResponseAsync(
            Arg.Is<IList<ChatMessage>>(messages => 
                messages.Any(m => m.Role == ChatRole.System && m.Text!.Contains("Central Time"))),
            Arg.Any<ChatOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ParseTimeAsync_ParsesValidUtcTimestamp()
    {
        var chatClient = Substitute.For<IChatClient>();
        var expectedTime = new DateTimeOffset(2025, 8, 8, 14, 0, 0, TimeSpan.Zero);
        var response = ChatResponseBuilder.Create()
            .WithAssistantText("2025-08-08T14:00:00Z")
            .Build();
        chatClient.GetResponseAsync(Arg.Any<IList<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var service = CreateService(chatClient: chatClient);

        var result = await service.ParseTimeAsync("tomorrow at 2pm");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value).IsEqualTo(expectedTime);
    }

    [Test]
    public async Task ParseTimeAsync_ParsesTimestampWithMilliseconds()
    {
        var chatClient = Substitute.For<IChatClient>();
        var expectedTime = new DateTimeOffset(2025, 12, 25, 10, 30, 45, 123, TimeSpan.Zero);
        var response = ChatResponseBuilder.Create()
            .WithAssistantText("2025-12-25T10:30:45.123Z")
            .Build();
        chatClient.GetResponseAsync(Arg.Any<IList<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var service = CreateService(chatClient: chatClient);

        var result = await service.ParseTimeAsync("Christmas morning");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Year).IsEqualTo(2025);
        await Assert.That(result.Value.Month).IsEqualTo(12);
        await Assert.That(result.Value.Day).IsEqualTo(25);
        await Assert.That(result.Value.Hour).IsEqualTo(10);
    }

    [Test]
    public async Task ParseTimeAsync_ThrowsWhenLlmReturnsEmpty()
    {
        var chatClient = Substitute.For<IChatClient>();
        var response = ChatResponseBuilder.Create()
            .WithAssistantText("")
            .Build();
        chatClient.GetResponseAsync(Arg.Any<IList<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var service = CreateService(chatClient: chatClient);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ParseTimeAsync("invalid"));
    }

    [Test]
    public async Task ParseTimeAsync_ThrowsWhenLlmReturnsInvalidFormat()
    {
        var chatClient = Substitute.For<IChatClient>();
        var response = ChatResponseBuilder.Create()
            .WithAssistantText("not a valid timestamp")
            .Build();
        chatClient.GetResponseAsync(Arg.Any<IList<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var service = CreateService(chatClient: chatClient);

        await Assert.ThrowsAsync<FormatException>(() => service.ParseTimeAsync("bad input"));
    }

    [Test]
    public async Task ParseTimeAsync_TrimsWhitespaceFromInput()
    {
        var chatClient = Substitute.For<IChatClient>();
        var response = ChatResponseBuilder.Create()
            .WithAssistantText("2025-08-08T14:00:00Z")
            .Build();
        chatClient.GetResponseAsync(Arg.Any<IList<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var service = CreateService(chatClient: chatClient);

        await service.ParseTimeAsync("  tomorrow at 2pm  ");

        await chatClient.Received(1).GetResponseAsync(
            Arg.Is<IList<ChatMessage>>(messages => 
                messages.Any(m => m.Role == ChatRole.User && m.Text == "tomorrow at 2pm")),
            Arg.Any<ChatOptions>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ParseTimeAsync_ReturnsUtcTime()
    {
        var chatClient = Substitute.For<IChatClient>();
        var response = ChatResponseBuilder.Create()
            .WithAssistantText("2025-06-15T18:30:00Z")
            .Build();
        chatClient.GetResponseAsync(Arg.Any<IList<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var service = CreateService(chatClient: chatClient);

        var result = await service.ParseTimeAsync("6:30 PM");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Offset).IsEqualTo(TimeSpan.Zero);
    }

    [Test]
    public async Task ParseTimeAsync_HandlesRelativeTime()
    {
        var chatClient = Substitute.For<IChatClient>();
        var futureTime = DateTimeOffset.UtcNow.AddHours(3);
        var response = ChatResponseBuilder.Create()
            .WithAssistantText(futureTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"))
            .Build();
        chatClient.GetResponseAsync(Arg.Any<IList<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var service = CreateService(chatClient: chatClient);

        var result = await service.ParseTimeAsync("in 3 hours");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value).IsGreaterThan(DateTimeOffset.UtcNow);
    }

    [Test]
    public async Task ParseTimeAsync_PreservesSecondsAndMilliseconds()
    {
        var chatClient = Substitute.For<IChatClient>();
        var response = ChatResponseBuilder.Create()
            .WithAssistantText("2025-03-20T15:45:30.500Z")
            .Build();
        chatClient.GetResponseAsync(Arg.Any<IList<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
            .Returns(response);

        var service = CreateService(chatClient: chatClient);

        var result = await service.ParseTimeAsync("specific time");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value.Second).IsEqualTo(30);
        await Assert.That(result.Value.Millisecond).IsEqualTo(500);
    }
}
