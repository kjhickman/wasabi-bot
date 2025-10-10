using WasabiBot.Api.Infrastructure.Discord.Interactions;
using Xunit;

namespace WasabiBot.UnitTests;

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
        return new InteractionResponder(
            threshold,
            defer: _ => { tracker.DeferCalls++; return Task.CompletedTask; },
            respond: (content, ephemeral) => { tracker.RespondCalls++; tracker.RespondMessages.Add((content, ephemeral)); return Task.CompletedTask; },
            followup: (content, ephemeral) => { tracker.FollowupCalls++; tracker.FollowupMessages.Add((content, ephemeral)); return Task.CompletedTask; }
        );
    }

    [Fact]
    public async Task FastSend_BeforeThreshold_UsesRespond()
    {
        var tracker = new CallTracker();
        await using var responder = CreateResponder(TimeSpan.FromSeconds(5), tracker);
        await responder.SendAsync("hello", ephemeral: true);
        Assert.Equal(0, tracker.DeferCalls);
        Assert.Equal(1, tracker.RespondCalls);
        Assert.Equal(0, tracker.FollowupCalls);
        Assert.Single(tracker.RespondMessages);
        Assert.True(tracker.RespondMessages.First().Ephemeral);
    }

    [Fact]
    public async Task SlowSend_AfterAutoDefer_UsesFollowup()
    {
        var tracker = new CallTracker();
        var threshold = TimeSpan.FromMilliseconds(50);
        await using var responder = CreateResponder(threshold, tracker);
        await Task.Delay(threshold + TimeSpan.FromMilliseconds(60), TestContext.Current.CancellationToken); // allow auto-defer to trigger
        await responder.SendAsync("work done");
        Assert.Equal(1, tracker.DeferCalls); // auto defer
        Assert.Equal(0, tracker.RespondCalls); // initial respond skipped
        Assert.Equal(1, tracker.FollowupCalls); // followup after defer
        Assert.Single(tracker.FollowupMessages);
    }

    [Fact]
    public async Task MultipleSends_FirstResponds_OthersFollowup()
    {
        var tracker = new CallTracker();
        await using var responder = CreateResponder(TimeSpan.FromSeconds(5), tracker);
        await responder.SendAsync("first");
        await responder.SendAsync("second");
        await responder.SendAsync("third");
        Assert.Equal(0, tracker.DeferCalls);
        Assert.Equal(1, tracker.RespondCalls);
        Assert.Equal(2, tracker.FollowupCalls);
        Assert.Equal(["first"], tracker.RespondMessages.Select(m => m.Content));
        Assert.Equal(["second", "third"], tracker.FollowupMessages.Select(m => m.Content));
    }

    [Fact]
    public async Task ParallelSends_OnlyOneResponds_RestFollowup()
    {
        var tracker = new CallTracker();
        await using var responder = CreateResponder(TimeSpan.FromSeconds(5), tracker);
        var sendTasks = Enumerable.Range(0, 10)
            .Select(i => responder.SendAsync($"msg-{i}"))
            .ToArray();
        await Task.WhenAll(sendTasks);
        Assert.Equal(1, tracker.RespondCalls);
        Assert.Equal(0, tracker.DeferCalls); // threshold not reached
        Assert.Equal(9, tracker.FollowupCalls);
    }

    [Fact]
    public async Task DisposeBeforeThreshold_CancelsTimer_NoDefer()
    {
        var tracker = new CallTracker();
        var responder = CreateResponder(TimeSpan.FromMilliseconds(200), tracker);
        await responder.DisposeAsync();
        await Task.Delay(250, TestContext.Current.CancellationToken);
        Assert.Equal(0, tracker.DeferCalls);
    }
}
