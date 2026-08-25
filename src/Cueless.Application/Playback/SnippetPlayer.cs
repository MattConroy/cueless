namespace Cueless.Application.Playback;

public sealed class SnippetPlayer(IMediaPlayer player, TimeProvider timeProvider)
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan CueTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan AudibleTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan StallAllowance = TimeSpan.FromSeconds(15);
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
            _ => player.State is PlaybackState.Cued,
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
            position => player.State == PlaybackState.Playing && Difference(position, offset) <= SeekTolerance,
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
        Func<TimeSpan, bool> reached,
        TimeSpan timeout,
        string failure,
        IProgress<SnippetProgress>? progress,
        CancellationToken cancellationToken)
    {
        var startedAtTimestamp = timeProvider.GetTimestamp();

        while (true)
        {
            EnsureStillPlayable();

            var position = player.Position;
            var elapsed = timeProvider.GetElapsedTime(startedAtTimestamp);
            var arrived = reached(position);

            // Playing something that is not the track we asked for means an
            // advertisement is in front of it.
            var advertising = !arrived && player.State == PlaybackState.Playing;

            progress?.Report(new SnippetProgress(phase, player.State, position, elapsed, advertising));

            if (arrived)
            {
                return position;
            }

            if (elapsed > timeout)
            {
                player.Pause();
                throw new SnippetPlaybackException(failure);
            }

            await Task.Delay(PollInterval, timeProvider, cancellationToken);
        }
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
}
