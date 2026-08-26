using Cueless.Application.Playback;
using Microsoft.Extensions.Time.Testing;

namespace Cueless.Application.UnitTests;

public class SnippetPlayerTests
{
    private static readonly TimeSpan Offset = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    // The loop only accepts the track as playing once the position moves on its own.
    private static readonly TimeSpan FirstAdvance = TimeSpan.FromSeconds(0.02);

    [Fact]
    public async Task PlaysBeforeMeasuring()
    {
        var player = new FakeMediaPlayer();
        player.Reports(Offset, Offset + FirstAdvance, Offset + FirstAdvance + OneSecond);

        await PlayAsync(player, new FakeTimeProvider(), TimeSpan.FromMilliseconds(16));

        Assert.True(player.Started);
    }

    [Fact]
    public async Task ARebufferingPlayerStillDeliversAFullSecondOfAudio()
    {
        var player = new FakeMediaPlayer();
        var stalled = Enumerable.Repeat(Offset + FirstAdvance, 20).ToArray();
        player.Reports([
            Offset,
            Offset + FirstAdvance,
            .. stalled,
            Offset + FirstAdvance + TimeSpan.FromSeconds(0.5),
            Offset + FirstAdvance + OneSecond]);

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
        player.Reports(Offset, Offset + FirstAdvance, Offset + FirstAdvance + OneSecond);

        await PlayAsync(player, new FakeTimeProvider(), TimeSpan.FromMilliseconds(16));

        Assert.True(player.Paused);
    }

    [Fact]
    public async Task PlaybackThatNeverAdvancesFails()
    {
        var player = new FakeMediaPlayer();
        player.Reports(Offset, Offset + FirstAdvance);

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
            Offset + FirstAdvance,
            Offset + FirstAdvance + OneSecond,
            Offset + FirstAdvance + OneSecond + TimeSpan.FromSeconds(0.12));

        var outcome = await PlayAsync(player, new FakeTimeProvider(), TimeSpan.FromMilliseconds(100));

        Assert.Equal(TimeSpan.FromSeconds(0.12), outcome.Overshoot);
        Assert.True(outcome.Delivered > outcome.Heard);
    }

    [Fact]
    public async Task APauseLeadStopsTheSnippetEarly()
    {
        var player = new FakeMediaPlayer();
        player.Reports(Offset, Offset + FirstAdvance, Offset + FirstAdvance + TimeSpan.FromSeconds(0.8));

        var outcome = await PlayAsync(
            player,
            new FakeTimeProvider(),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(250));

        Assert.True(outcome.Heard < OneSecond, $"stopped at {outcome.Heard.TotalSeconds:0.00}s");
    }

    [Fact]
    public async Task ALongAdvertisementDoesNotTimeTheWaitOut()
    {
        var player = new FakeMediaPlayer();
        var longAdvertisement = Enumerable.Repeat(TimeSpan.FromSeconds(3), 60).ToArray();
        player.Reports([.. longAdvertisement, Offset, Offset, Offset + OneSecond]);

        var outcome = await PlayAsync(player, new FakeTimeProvider(), TimeSpan.FromSeconds(1));

        Assert.Equal(Offset, outcome.StartedAt);
    }

    [Fact]
    public async Task APositionThatStandsStillWhilePlayingIsReportedAsAnAdvertisement()
    {
        var player = new FakeMediaPlayer();
        var frozenAtTheOffset = Enumerable.Repeat(Offset, 30).ToArray();
        player.Reports([
            .. frozenAtTheOffset,
            Offset + TimeSpan.FromSeconds(0.5),
            Offset + TimeSpan.FromSeconds(0.5) + OneSecond]);

        var samples = new RecordingProgress();
        await PlayAsync(player, new FakeTimeProvider(), TimeSpan.FromMilliseconds(100), progress: samples);

        Assert.True(
            samples.Samples.Exists(sample => sample.Advertising),
            string.Join("\n", samples.Samples.Select(sample =>
                $"{sample.Phase} state={sample.State} pos={sample.Position.TotalSeconds:0.00} elapsed={sample.Elapsed.TotalSeconds:0.00} ad={sample.Advertising}")));
    }

    [Fact]
    public async Task AnAdvertisementReportingItsOwnPositionWhileUnstartedIsStillAnAdvertisement()
    {
        var player = new FakeMediaPlayer { State = PlaybackState.Unstarted };
        var advertisement = Enumerable.Range(1, 20).Select(second => TimeSpan.FromSeconds(second * 0.2)).ToArray();
        player.Reports([.. advertisement, Offset, Offset + FirstAdvance, Offset + FirstAdvance + OneSecond]);

        var samples = new RecordingProgress();
        await PlayAsync(player, new FakeTimeProvider(), TimeSpan.FromMilliseconds(100), progress: samples);

        Assert.Contains(samples.Samples, sample => sample.Advertising);
    }

    private static async Task<SnippetOutcome> PlayAsync(
        FakeMediaPlayer player,
        FakeTimeProvider time,
        TimeSpan step,
        TimeSpan lead = default,
        IProgress<SnippetProgress>? progress = null)
    {
        var snippetPlayer = new SnippetPlayer(player, time);
        var playing = snippetPlayer.PlayAsync(Offset, OneSecond, lead, progress, CancellationToken.None);

        for (var tick = 0; tick < 2000 && !playing.IsCompleted; tick++)
        {
            time.Advance(step);
            await Task.Yield();
        }

        Assert.True(playing.IsCompleted, "the snippet loop did not finish");

        return await playing;
    }
}
