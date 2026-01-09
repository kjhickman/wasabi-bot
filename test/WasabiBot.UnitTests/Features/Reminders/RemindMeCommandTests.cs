// using Microsoft.Extensions.AI;
// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Logging.Abstractions;
// using NSubstitute;
// using OpenTelemetry.Trace;
// using WasabiBot.Api.Features.Reminders;
// using WasabiBot.Api.Features.Reminders.Abstractions;
// using WasabiBot.UnitTests.Builders;
// using WasabiBot.UnitTests.Infrastructure.Discord;

// namespace WasabiBot.UnitTests.Features.Reminders;

// public class RemindMeCommandTests
// {
//     private static RemindMeCommand CreateCommand(
//         IChatClient chatClient,
//         IServiceScopeFactory scopeFactory,
//         IReminderTimeCalculator timeCalculator)
//     {
//         var tracer = TracerProvider.Default.GetTracer("remindme-tests");
//         return new RemindMeCommand(chatClient, tracer, scopeFactory, timeCalculator, NullLogger<RemindMeCommand>.Instance, TimeProvider.System);
//     }

//     private static IServiceScopeFactory CreateScopeFactory(IReminderService reminderService)
//     {
//         var scopeFactory = Substitute.For<IServiceScopeFactory>();
//         var scope = Substitute.For<IServiceScope>();
//         var provider = Substitute.For<IServiceProvider>();
//         provider.GetService(typeof(IReminderService)).Returns(reminderService);
//         scope.ServiceProvider.Returns(provider);
//         scopeFactory.CreateScope().Returns(scope);
//         return scopeFactory;
//     }

//     private static IChatClient CreateChatClient(ChatResponse response)
//     {
//         var chatClient = Substitute.For<IChatClient>();
//         chatClient
//             .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
//             .Returns(Task.FromResult(response));
//         return chatClient;
//     }

//     [Test]
//     public async Task ExecuteAsync_WhenScheduleSucceeds_RespondsWithConfirmation()
//     {
//         var reminderService = Substitute.For<IReminderService>();
//         reminderService
//             .ScheduleAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
//             .Returns(Task.FromResult(true));
//         var scopeFactory = CreateScopeFactory(reminderService);

//         var targetTime = DateTimeOffset.UtcNow.AddMinutes(30);
//         var chatResponse = ChatResponseBuilder.Create()
//             .WithToolResult("RelativeTime", targetTime.ToString("O"))
//             .Build();
//         var chatClient = CreateChatClient(chatResponse);
//         var command = CreateCommand(chatClient, scopeFactory, Substitute.For<IReminderTimeCalculator>());

//         var context = new FakeCommandContext(userId: 42, channelId: 9001, userDisplayName: "ReminderUser");

//         await command.ExecuteAsync(context, "in 30 minutes", "stretch");

//         await reminderService.Received(1).ScheduleAsync(
//             42UL,
//             9001UL,
//             "stretch",
//             Arg.Is<DateTimeOffset>(d => d == targetTime));

//         await Assert.That(context.Messages.Count).IsEqualTo(1);
//         var (message, ephemeral) = context.Messages.Single();
//         await Assert.That(ephemeral).IsFalse();
//         await Assert.That(message.Contains("<t:")).IsTrue();
//         await Assert.That(message.Contains("stretch")).IsTrue();
//     }

//     [Test]
//     public async Task ExecuteAsync_WhenToolMessageMissing_SendsToolFailure()
//     {
//         var reminderService = Substitute.For<IReminderService>();
//         var scopeFactory = CreateScopeFactory(reminderService);

//         var chatResponse = ChatResponseBuilder.Create()
//             .WithAssistantText("no tools used")
//             .Build();
//         var chatClient = CreateChatClient(chatResponse);
//         var command = CreateCommand(chatClient, scopeFactory, Substitute.For<IReminderTimeCalculator>());

//         var context = new FakeCommandContext();

//         await command.ExecuteAsync(context, "soon", "hydrate");

//         await reminderService.DidNotReceive().ScheduleAsync(
//             Arg.Any<ulong>(),
//             Arg.Any<ulong>(),
//             Arg.Any<string>(),
//             Arg.Any<DateTimeOffset>());

//         var ephemerals = context.EphemeralMessages;
//         await Assert.That(ephemerals.Count).IsEqualTo(1);
//         await Assert.That(ephemerals.Single()).IsEqualTo("Failed to get a response from the AI tool.");
//     }

//     [Test]
//     public async Task ExecuteAsync_WhenToolResultEmpty_SendsInterpretationFailure()
//     {
//         var reminderService = Substitute.For<IReminderService>();
//         var scopeFactory = CreateScopeFactory(reminderService);

//         var chatResponse = ChatResponseBuilder.Create()
//             .WithToolResult("RelativeTime", string.Empty)
//             .Build();
//         var chatClient = CreateChatClient(chatResponse);
//         var command = CreateCommand(chatClient, scopeFactory, Substitute.For<IReminderTimeCalculator>());

//         var context = new FakeCommandContext();

//         await command.ExecuteAsync(context, "in 5 minutes", "refill water");

//         await reminderService.DidNotReceive().ScheduleAsync(
//             Arg.Any<ulong>(),
//             Arg.Any<ulong>(),
//             Arg.Any<string>(),
//             Arg.Any<DateTimeOffset>());

//         var ephemerals = context.EphemeralMessages;
//         await Assert.That(ephemerals.Count).IsEqualTo(1);
//         await Assert.That(ephemerals.Single()).IsEqualTo("I couldn't interpret that timeframe.");
//     }

//     [Test]
//     public async Task ExecuteAsync_WhenToolResultIsInvalidTimestamp_SendsParseFailure()
//     {
//         var reminderService = Substitute.For<IReminderService>();
//         var scopeFactory = CreateScopeFactory(reminderService);

//         var chatResponse = ChatResponseBuilder.Create()
//             .WithToolResult("RelativeTime", "not-a-timestamp")
//             .Build();
//         var chatClient = CreateChatClient(chatResponse);
//         var command = CreateCommand(chatClient, scopeFactory, Substitute.For<IReminderTimeCalculator>());

//         var context = new FakeCommandContext();

//         await command.ExecuteAsync(context, "later", "check oven");

//         await reminderService.DidNotReceive().ScheduleAsync(
//             Arg.Any<ulong>(),
//             Arg.Any<ulong>(),
//             Arg.Any<string>(),
//             Arg.Any<DateTimeOffset>());

//         var ephemerals = context.EphemeralMessages;
//         await Assert.That(ephemerals.Count).IsEqualTo(1);
//         await Assert.That(ephemerals.Single()).IsEqualTo("Something went wrong interpreting that timeframe.");
//     }

//     [Test]
//     public async Task ExecuteAsync_WhenTimestampIsNotInFuture_SendsFutureOnlyWarning()
//     {
//         var reminderService = Substitute.For<IReminderService>();
//         var scopeFactory = CreateScopeFactory(reminderService);

//         var targetTime = DateTimeOffset.UtcNow.AddMinutes(-2);
//         var chatResponse = ChatResponseBuilder.Create()
//             .WithToolResult("RelativeTime", targetTime.ToString("O"))
//             .Build();
//         var chatClient = CreateChatClient(chatResponse);
//         var command = CreateCommand(chatClient, scopeFactory, Substitute.For<IReminderTimeCalculator>());

//         var context = new FakeCommandContext();

//         await command.ExecuteAsync(context, "yesterday", "ping friend");

//         await reminderService.DidNotReceive().ScheduleAsync(
//             Arg.Any<ulong>(),
//             Arg.Any<ulong>(),
//             Arg.Any<string>(),
//             Arg.Any<DateTimeOffset>());

//         var ephemerals = context.EphemeralMessages;
//         await Assert.That(ephemerals.Count).IsEqualTo(1);
//         await Assert.That(ephemerals.Single()).IsEqualTo("Only future times are allowed.");
//     }

//     [Test]
//     public async Task ExecuteAsync_WhenSchedulingFails_SendsPersistenceFailure()
//     {
//         var reminderService = Substitute.For<IReminderService>();
//         reminderService
//             .ScheduleAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>())
//             .Returns(Task.FromResult(false));
//         var scopeFactory = CreateScopeFactory(reminderService);

//         var targetTime = DateTimeOffset.UtcNow.AddMinutes(10);
//         var chatResponse = ChatResponseBuilder.Create()
//             .WithToolResult("RelativeTime", targetTime.ToString("O"))
//             .Build();
//         var chatClient = CreateChatClient(chatResponse);
//         var command = CreateCommand(chatClient, scopeFactory, Substitute.For<IReminderTimeCalculator>());

//         var context = new FakeCommandContext();

//         await command.ExecuteAsync(context, "in 10 minutes", "take break");

//         var ephemerals = context.EphemeralMessages;
//         await Assert.That(ephemerals.Count).IsEqualTo(1);
//         await Assert.That(ephemerals.Single()).IsEqualTo("Reminder failed to save. Please try again later.");
//     }

//     [Test]
//     public async Task ExecuteAsync_WhenChatClientThrows_SendsGenericFailure()
//     {
//         var reminderService = Substitute.For<IReminderService>();
//         var scopeFactory = CreateScopeFactory(reminderService);

//         var chatClient = Substitute.For<IChatClient>();
//         chatClient
//             .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions>(), Arg.Any<CancellationToken>())
//             .Returns(Task.FromException<ChatResponse>(new InvalidOperationException("boom")));
//         var command = CreateCommand(chatClient, scopeFactory, Substitute.For<IReminderTimeCalculator>());

//         var context = new FakeCommandContext();

//         await command.ExecuteAsync(context, "in an hour", "water plants");

//         var ephemerals = context.EphemeralMessages;
//         await Assert.That(ephemerals.Count).IsEqualTo(1);
//         await Assert.That(ephemerals.Single()).IsEqualTo("Failed to process reminder.");
//     }
// }
