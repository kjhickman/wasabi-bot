using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NetCord;
using NetCord.JsonModels;
using NSubstitute;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.CaptionThis;
using WasabiBot.Api.Features.CaptionThis.Abstractions;
using WasabiBot.Api.Infrastructure.AI;
using WasabiBot.UnitTests.Builders;
using WasabiBot.UnitTests.Infrastructure.Discord;

namespace WasabiBot.UnitTests.Features.CaptionThis;

public class CaptionThisCommandTests
{
    private static CaptionThisCommand CreateCommand(IChatClient chatClient, IImageRetrievalService imageRetrievalService)
    {
        var factory = Substitute.For<IChatClientFactory>();
        factory.GetChatClient(Arg.Any<LlmPreset>()).Returns(chatClient);
        var tracer = TracerProvider.Default.GetTracer("caption-tests");
        return new CaptionThisCommand(factory, imageRetrievalService, tracer, NullLogger<CaptionThisCommand>.Instance);
    }

    [Test]
    public async Task ExecuteAsync_WithValidImage_RespondsWithCaption()
    {
        var chatResponse = ChatResponseBuilder.Create()
            .WithAssistantText("A hilarious caption")
            .Build();

        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>())
            .Returns(Task.FromResult(chatResponse));

        var imageRetrievalService = Substitute.For<IImageRetrievalService>();
        imageRetrievalService
            .GetImageBytesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new byte[] { 1, 2, 3 }));

        var command = CreateCommand(chatClient, imageRetrievalService);
        var context = new FakeCommandContext();

        var attachment = CreateAttachment("meme.png", "image/png", 1024, "http://example.com/meme.png");

        await command.ExecuteAsync(context, attachment);

        await chatClient.Received(1).GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>());
        await Assert.That(context.Messages.Count).IsEqualTo(1);
        var (message, ephemeral) = context.Messages.Single();
        await Assert.That(ephemeral).IsFalse();
        await Assert.That(message.Contains("A hilarious caption")).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_WithUnsupportedImageType_SendsError()
    {
        var chatClient = Substitute.For<IChatClient>();
        var imageRetrievalService = Substitute.For<IImageRetrievalService>();

        var command = CreateCommand(chatClient, imageRetrievalService);
        var context = new FakeCommandContext();

        var attachment = CreateAttachment("document.pdf", "application/pdf", 512, "http://example.com/doc.pdf");

        await command.ExecuteAsync(context, attachment);

        await chatClient.DidNotReceive().GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>());
        await imageRetrievalService.DidNotReceive().GetImageBytesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        var ephemerals = context.EphemeralMessages;
        await Assert.That(ephemerals.Count).IsEqualTo(1);
        await Assert.That(ephemerals.Single()).IsEqualTo("Please provide a valid image file (jpg, jpeg, png, gif, webp).");
    }

    private static Attachment CreateAttachment(string fileName, string? contentType, int size, string url)
    {
        var json = new JsonAttachment
        {
            Id = 1,
            FileName = fileName,
            ContentType = contentType,
            Size = size,
            Url = url
        };

        return new Attachment(json);
    }
}
