using Cueless.Application.Playback;
using Microsoft.Extensions.Time.Testing;

namespace Cueless.Application.UnitTests;

public class SnippetPlayerTests
{
    private static readonly TimeSpan Offset = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task PlaysBeforeMeasuring()
    {
        var player = new FakeMediaPlayer();
        player.Reports(Offset, Offset, Offset + OneSecond);

        await PlayAsync(player, new FakeTimeProvider(), TimeSpan.FromMilliseconds(16));

        Assert.True(player.Started);
    }

    [Fact]
    public async Task ARebufferingPlayerStillDeliversAFullSecondOfAudio()
    {
        var player = new FakeMediaPlayer();
        var stalled = Enumerable.Repeat(Offset, 20).ToArray();
        player.Reports([Offset, .. stalled, Offset + TimeSpan.FromSeconds(0.5), Offset + OneSecond]);

        var time = new FakeTimeProvider();
        var outcome = await PlayAsync(player, time, TimeSpan.FromMilliseconds(100));

        Assert.True(outcome.Heard >= OneSecond, $"only {outcome.Heard.TotalSeconds:0.00}s was heard");
        Assert.True(outcome.Elapsed > outcome.Heard, "the stall should show as wall-clock time beyond the audio heard");
        Assert.True(outcome.Stalled > TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task AnAdvertisementDoesNotConsumeTheSnippet()
    {
        var player = new FakeMediaPlayer();
        var advertisement = new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) };
        player.Reports([.. advertisement, Offset, Offset, Offset + OneSecond]);

        var outcome = await PlayAsync(player, new FakeTimeProvider(), TimeSpan.FromMilliseconds(100));

        Assert.Equal(Offset, outcome.StartedAt);
        Assert.True(outcome.Heard >= OneSecond);
    }

    [Fact]
    public async Task PausesOnceTheSnippetHasBeenHeard()
    {
        var player = new FakeMediaPlayer();
        player.Reports(Offset, Offset, Offset + OneSecond);

        await PlayAsync(player, new FakeTimeProvider(), TimeSpan.FromMilliseconds(16));

        Assert.True(player.Paused);
    }

    [Fact]
    public async Task PlaybackThatNeverAdvancesFails()
    {
        var player = new FakeMediaPlayer();
        player.Reports(Offset);

        var failure = await Assert.ThrowsAsync<SnippetPlaybackException>(
            () => PlayAsync(player, new FakeTimeProvider(), TimeSpan.FromSeconds(1)));

        Assert.Contains("stalling", failure.Message, StringComparison.Ordinal);
        Assert.True(player.Paused);
    }

    [Fact]
    public async Task AnUnavailableVideoFails()
    {
        var player = new FakeMediaPlayer { State = PlaybackState.Unavailable };

        var failure = await Assert.ThrowsAsync<SnippetPlaybackException>(
            () => PlayAsync(player, new FakeTimeProvider(), TimeSpan.FromMilliseconds(16)));

        Assert.Contains("unavailable", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NeverReachingTheOffsetFails()
    {
        var player = new FakeMediaPlayer();
        player.Reports(TimeSpan.Zero);

        var failure = await Assert.ThrowsAsync<SnippetPlaybackException>(
            () => PlayAsync(player, new FakeTimeProvider(), TimeSpan.FromSeconds(1)));

        Assert.Contains("never reached", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AudioPlayedAfterThePauseIsReportedAsOvershoot()
    {
        var player = new FakeMediaPlayer();
        player.Reports(
            Offset,
            Offset,
            Offset + OneSecond,
            Offset + OneSecond + TimeSpan.FromSeconds(0.12));

        var outcome = await PlayAsync(player, new FakeTimeProvider(), TimeSpan.FromMilliseconds(100));

        Assert.Equal(TimeSpan.FromSeconds(0.12), outcome.Overshoot);
        Assert.True(outcome.Delivered > outcome.Heard);
    }

    [Fact]
    public async Task APauseLeadStopsTheSnippetEarly()
    {
        var player = new FakeMediaPlayer();
        player.Reports(Offset, Offset, Offset + TimeSpan.FromSeconds(0.8));

        var outcome = await PlayAsync(
            player,
            new FakeTimeProvider(),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(250));

        Assert.True(outcome.Heard < OneSecond, $"stopped at {outcome.Heard.TotalSeconds:0.00}s");
    }

    private static async Task<SnippetOutcome> PlayAsync(
        FakeMediaPlayer player,
        FakeTimeProvider time,
        TimeSpan step,
        TimeSpan lead = default)
    {
        var snippetPlayer = new SnippetPlayer(player, time);
        var playing = snippetPlayer.PlayAsync(Offset, OneSecond, lead, null, CancellationToken.None);

        for (var tick = 0; tick < 2000 && !playing.IsCompleted; tick++)
        {
            time.Advance(step);
            await Task.Yield();
        }

        Assert.True(playing.IsCompleted, "the snippet loop did not finish");

        return await playing;
    }
}
