using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.Ask;
using WasabiBot.UnitTests.Builders;
using WasabiBot.UnitTests.Infrastructure.Discord;

namespace WasabiBot.UnitTests.Features.Ask;

public class AskCommandTests
{
    private static AskCommand CreateCommand(IChatClient chatClient)
    {
        var tracer = TracerProvider.Default.GetTracer("ask-tests");
        return new AskCommand(chatClient, tracer, NullLogger<AskCommand>.Instance);
    }

    [Test]
    public async Task ExecuteAsync_WhenResponseReceived_UsesChatClientAndSendsReply()
    {
        var chatResponse = ChatResponseBuilder.Create()
            .WithAssistantText("Short answer")
            .Build();

        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>())
            .Returns(Task.FromResult(chatResponse));

        var command = CreateCommand(chatClient);
        var context = new FakeCommandContext();

        const string question = "How many moons does Mars have?";
        await command.ExecuteAsync(context, question);

        await chatClient.Received(1).GetResponseAsync(
            Arg.Is<IEnumerable<ChatMessage>>(messages => HasExpectedPrompt(messages, question)));

        await Assert.That(context.Messages.Count).IsEqualTo(1);
        var (message, ephemeral) = context.Messages.Single();
        await Assert.That(ephemeral).IsFalse();
        await Assert.That(message).IsEqualTo($"**Question:** {question}\n\nShort answer");
    }

    [Test]
    public async Task ExecuteAsync_WhenChatClientThrows_SendsFriendlyError()
    {
        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>())
            .Returns(Task.FromException<ChatResponse>(new InvalidOperationException("outage")));

        var command = CreateCommand(chatClient);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context, "What time is it?");

        await Assert.That(context.EphemeralMessages.Count).IsEqualTo(1);
        await Assert.That(context.EphemeralMessages.Single()).IsEqualTo("I couldn't answer that right now. Please try again later.");
    }

    private static bool HasExpectedPrompt(IEnumerable<ChatMessage> messages, string question)
    {
        var list = messages.ToList();

        return list.Count == 2
               && list[0].Role == ChatRole.System
               && list[0].Text.Contains("Keep the reply short and concise.")
               && list[0].Text.Contains("There will be no follow-up conversation")
               && list[1].Role == ChatRole.User
               && list[1].Text == question;
    }
}
