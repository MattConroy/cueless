namespace Cueless.Application.Playback;

public sealed class SnippetPlayer(IMediaPlayer player, TimeProvider timeProvider)
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan CueTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan AudibleTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan StallAllowance = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan AdvertisementAllowance = TimeSpan.FromMinutes(2);

    // The reported position stands still while a pre-roll plays, because it belongs to
    // the track underneath rather than the advertisement in front of it.
    private static readonly TimeSpan StillnessBeforeAdvertisement = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan Advance = TimeSpan.FromMilliseconds(5);
    private static readonly TimeSpan PauseSettleDelay = TimeSpan.FromMilliseconds(250);

    // An advertisement reports its own current time, so playback is only the track
    // once the reported position is near where we asked to be.
    private static readonly TimeSpan SeekTolerance = TimeSpan.FromSeconds(2);

    // Cueing at the offset buffers there without playing, so the tap that follows
    // starts audio immediately and stays inside the browser's gesture allowance.
    public async Task PrepareAsync(
        string videoIdentifier,
        TimeSpan offset,
        IProgress<SnippetProgress>? progress,
        CancellationToken cancellationToken)
    {
        player.Cue(videoIdentifier, offset);

        _ = await WaitUntilAsync(
            "cueing",
            (_, _) => player.State is PlaybackState.Cued ? WaitVerdict.Arrived : WaitVerdict.Waiting,
            CueTimeout,
            "The player never finished cueing the video.",
            progress,
            cancellationToken);
    }

    // Seeking a video that is already loaded avoids a reload, and so avoids the
    // pre-roll that a reload would bring with it.
    public async Task<SnippetOutcome> PlayAsync(
        TimeSpan offset,
        TimeSpan length,
        TimeSpan pauseLead,
        IProgress<SnippetProgress>? progress,
        CancellationToken cancellationToken)
    {
        player.Seek(offset);
        player.Play();

        var startedAt = await WaitUntilAsync(
            "waiting for audio",
            (position, moving) => Assess(position, moving, offset),
            AudibleTimeout,
            "The player never reached the requested offset.",
            progress,
            cancellationToken);

        var startedAtTimestamp = timeProvider.GetTimestamp();
        var backstop = length + StallAllowance;
        var stopAt = length - pauseLead;
        TimeSpan heard;
        TimeSpan elapsed;

        while (true)
        {
            var position = player.Position;
            heard = position - startedAt;
            elapsed = timeProvider.GetElapsedTime(startedAtTimestamp);

            progress?.Report(new SnippetProgress("measuring", player.State, position, elapsed, false));

            if (heard >= stopAt)
            {
                break;
            }

            if (elapsed > backstop)
            {
                player.Pause();
                throw new SnippetPlaybackException(
                    $"Playback delivered {heard.TotalSeconds:0.00}s of the {length.TotalSeconds:0.00}s snippet before stalling.");
            }

            EnsureStillPlayable();

            await Task.Delay(PollInterval, timeProvider, cancellationToken);
        }

        player.Pause();
        await Task.Delay(PauseSettleDelay, timeProvider, cancellationToken);

        var delivered = player.Position - startedAt;

        return new SnippetOutcome(length, heard, delivered, elapsed, startedAt);
    }

    private async Task<TimeSpan> WaitUntilAsync(
        string phase,
        Func<TimeSpan, bool, WaitVerdict> assess,
        TimeSpan timeout,
        string failure,
        IProgress<SnippetProgress>? progress,
        CancellationToken cancellationToken)
    {
        var startedAtTimestamp = timeProvider.GetTimestamp();
        var deadline = startedAtTimestamp;
        var furthest = TimeSpan.MinValue;
        var movedAt = startedAtTimestamp;
        var advanced = false;

        while (true)
        {
            EnsureStillPlayable();

            var position = player.Position;
            var elapsed = timeProvider.GetElapsedTime(startedAtTimestamp);

            if (furthest == TimeSpan.MinValue)
            {
                furthest = position;
            }
            else if (position > furthest + Advance)
            {
                furthest = position;
                movedAt = timeProvider.GetTimestamp();
                advanced = true;
            }

            // A pre-roll leaves the reported position where it was, so the track is
            // only really playing once that position has moved on its own.
            var moving = advanced && timeProvider.GetElapsedTime(movedAt) < StillnessBeforeAdvertisement;
            var verdict = assess(position, moving);
            var arrived = verdict is WaitVerdict.Arrived;
            var advertising = verdict is WaitVerdict.Advertising;

            progress?.Report(new SnippetProgress(phase, player.State, position, elapsed, advertising));

            if (arrived)
            {
                return position;
            }

            // An advertisement can run longer than the wait allows, and the viewer may
            // need time to reach the skip control, so it does not count against it.
            // The overall deadline still applies, so a player that is playing something
            // it never leaves cannot wait for ever.
            if (advertising)
            {
                startedAtTimestamp = timeProvider.GetTimestamp();
            }
            else if (elapsed > timeout)
            {
                player.Pause();
                throw new SnippetPlaybackException(failure);
            }

            if (timeProvider.GetElapsedTime(deadline) > AdvertisementAllowance)
            {
                player.Pause();
                throw new SnippetPlaybackException(failure);
            }

            await Task.Delay(PollInterval, timeProvider, cancellationToken);
        }
    }

    // A pre-roll is reported inconsistently: some players give the advertisement's own
    // position while the state stays unstarted, others leave the position on the track
    // and stand still. Neither is the track, and the state alone cannot tell them apart.
    private WaitVerdict Assess(TimeSpan position, bool moving, TimeSpan offset)
    {
        var atTheOffset = Difference(position, offset) <= SeekTolerance;

        if (atTheOffset && moving)
        {
            return WaitVerdict.Arrived;
        }

        return !atTheOffset || player.State == PlaybackState.Playing
            ? WaitVerdict.Advertising
            : WaitVerdict.Waiting;
    }

    private void EnsureStillPlayable()
    {
        if (player.State is PlaybackState.Unavailable)
        {
            throw new SnippetPlaybackException("The video is unavailable.");
        }
    }

    private static TimeSpan Difference(TimeSpan left, TimeSpan right) =>
        left > right ? left - right : right - left;

    private enum WaitVerdict
    {
        Waiting,
        Arrived,
        Advertising,
    }
}
