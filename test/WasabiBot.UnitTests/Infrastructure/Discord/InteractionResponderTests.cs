using WasabiBot.Api.Infrastructure.Discord.Interactions;

namespace WasabiBot.UnitTests.Infrastructure.Discord;

public class InteractionResponderTests
{
    private sealed class CallTracker
    {
        public int DeferCalls; // state 1
        public int RespondCalls; // state 2 initial response
        public int FollowupCalls; // state >=1 after ack
        public readonly List<(string Content, bool Ephemeral)> RespondMessages = [];
        public readonly List<(string Content, bool Ephemeral)> FollowupMessages = [];
    }

    private static InteractionResponder CreateResponder(TimeSpan threshold, CallTracker tracker)
    {
        var deferCutoff = DateTimeOffset.UtcNow + threshold;
        return new InteractionResponder(deferCutoff, defer: _ =>
        {
            tracker.DeferCalls++;
            return Task.CompletedTask;
        }, respond: (content, ephemeral) =>
        {
            tracker.RespondCalls++;
            tracker.RespondMessages.Add((content, ephemeral));
            return Task.CompletedTask;
        }, followup: (content, ephemeral) =>
        {
            tracker.FollowupCalls++;
            tracker.FollowupMessages.Add((content, ephemeral));
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task FastSend_BeforeThreshold_UsesRespond()
    {
        var tracker = new CallTracker();
        await using var responder = CreateResponder(TimeSpan.FromSeconds(5), tracker);
        await responder.SendAsync("hello", ephemeral: true);
        await Assert.That(tracker.DeferCalls).IsEqualTo(0);
        await Assert.That(tracker.RespondCalls).IsEqualTo(1);
        await Assert.That(tracker.FollowupCalls).IsEqualTo(0);
        await Assert.That(tracker.RespondMessages.Count).IsEqualTo(1);
        await Assert.That(tracker.RespondMessages.First().Ephemeral).IsTrue();
    }

    [Test]
    public async Task SlowSend_AfterAutoDefer_UsesFollowup()
    {
        var tracker = new CallTracker();
        var threshold = TimeSpan.FromMilliseconds(50);
        await using var responder = CreateResponder(threshold, tracker);
        await Task.Delay(threshold + TimeSpan.FromMilliseconds(60)); // allow auto-defer to trigger
        await responder.SendAsync("work done");
        await Assert.That(tracker.DeferCalls).IsEqualTo(1); // auto defer
        await Assert.That(tracker.RespondCalls).IsEqualTo(0); // initial respond skipped
        await Assert.That(tracker.FollowupCalls).IsEqualTo(1); // followup after defer
        await Assert.That(tracker.FollowupMessages.Count).IsEqualTo(1);
    }

    [Test]
    public async Task MultipleSends_FirstResponds_OthersFollowup()
    {
        var tracker = new CallTracker();
        await using var responder = CreateResponder(TimeSpan.FromSeconds(5), tracker);
        await responder.SendAsync("first");
        await responder.SendAsync("second");
        await responder.SendAsync("third");
        await Assert.That(tracker.DeferCalls).IsEqualTo(0);
        await Assert.That(tracker.RespondCalls).IsEqualTo(1);
        await Assert.That(tracker.FollowupCalls).IsEqualTo(2);
        await Assert.That(tracker.RespondMessages.Select(m => m.Content).ToArray()).IsEquivalentTo(["first"]);
        await Assert.That(tracker.FollowupMessages.Select(m => m.Content).ToArray()).IsEquivalentTo(["second", "third"]);
    }

    [Test]
    public async Task ParallelSends_OnlyOneResponds_RestFollowup()
    {
        var tracker = new CallTracker();
        await using var responder = CreateResponder(TimeSpan.FromSeconds(5), tracker);
        var sendTasks = Enumerable.Range(0, 10).Select(i => responder.SendAsync($"msg-{i}")).ToArray();
        await Task.WhenAll(sendTasks);
        await Assert.That(tracker.RespondCalls).IsEqualTo(1);
        await Assert.That(tracker.DeferCalls).IsEqualTo(0); // threshold not reached
        await Assert.That(tracker.FollowupCalls).IsEqualTo(9);
    }

    [Test]
    public async Task DisposeBeforeThreshold_CancelsTimer_NoDefer()
    {
        var tracker = new CallTracker();
        var responder = CreateResponder(TimeSpan.FromMilliseconds(200), tracker);
        await responder.DisposeAsync();
        await Task.Delay(250);
        await Assert.That(tracker.DeferCalls).IsEqualTo(0);
    }
}
