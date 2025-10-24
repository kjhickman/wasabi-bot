using System.Net;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenTelemetry.Trace;
using WasabiBot.Api.Features.CaptionThis;
using WasabiBot.UnitTests.Builders;
using WasabiBot.UnitTests.Infrastructure.Discord;

namespace WasabiBot.UnitTests.Features.CaptionThis;

public class CaptionThisCommandTests
{
    private sealed class StaticBytesHandler : HttpMessageHandler
    {
        private readonly byte[] _payload;

        public StaticBytesHandler(byte[] payload)
        {
            _payload = payload;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_payload)
            };
            return Task.FromResult(response);
        }
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public TestHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private static CaptionThisCommand CreateCommand(IChatClient chatClient, IHttpClientFactory httpClientFactory)
    {
        var tracer = TracerProvider.Default.GetTracer("caption-tests");
        return new CaptionThisCommand(chatClient, httpClientFactory, tracer, NullLogger<CaptionThisCommand>.Instance);
    }

    [Test]
    public async Task ExecuteAsync_WithValidImage_RespondsWithCaption()
    {
        var imageBytes = new byte[] { 1, 2, 3 };
        var handler = new StaticBytesHandler(imageBytes);
        var httpClient = new HttpClient(handler, disposeHandler: true);
        var httpClientFactory = new TestHttpClientFactory(httpClient);

        var chatResponse = ChatResponseBuilder.Create()
            .WithAssistantText("A hilarious caption")
            .Build();

        var chatClient = Substitute.For<IChatClient>();
        chatClient
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>())
            .Returns(Task.FromResult(chatResponse));

        var command = CreateCommand(chatClient, httpClientFactory);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context, "meme.png", "image/png", 1024, "http://example.com/meme.png");

        await chatClient.Received(1).GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>());
        await Assert.That(context.Messages.Count).IsEqualTo(1);
        var (message, ephemeral) = context.Messages.Single();
        await Assert.That(ephemeral).IsFalse();
        await Assert.That(message.Contains("A hilarious caption")).IsTrue();
    }

    [Test]
    public async Task ExecuteAsync_WithUnsupportedImageType_SendsError()
    {
        var httpClientFactory = new TestHttpClientFactory(new HttpClient());
        var chatClient = Substitute.For<IChatClient>();

        var command = CreateCommand(chatClient, httpClientFactory);
        var context = new FakeCommandContext();

        await command.ExecuteAsync(context, "document.pdf", "application/pdf", 512, "http://example.com/doc.pdf");

        await chatClient.DidNotReceive().GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>());
        var ephemerals = context.EphemeralMessages;
        await Assert.That(ephemerals.Count).IsEqualTo(1);
        await Assert.That(ephemerals.Single()).IsEqualTo("Please provide a valid image file (jpg, jpeg, png, gif, webp).");
    }
}
