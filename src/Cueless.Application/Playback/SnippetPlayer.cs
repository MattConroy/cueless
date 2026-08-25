namespace Cueless.Application.Playback;

public sealed class SnippetPlayer(IMediaPlayer player, TimeProvider timeProvider)
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan AudibleTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StallAllowance = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PauseSettleDelay = TimeSpan.FromMilliseconds(250);

    // An advertisement reports its own current time, so playback is only the track
    // once the reported position is near where we asked to be.
    private static readonly TimeSpan SeekTolerance = TimeSpan.FromSeconds(2);

    public async Task<SnippetOutcome> PlayAsync(
        string videoIdentifier,
        TimeSpan offset,
        TimeSpan length,
        CancellationToken cancellationToken)
    {
        player.Cue(videoIdentifier);
        player.Seek(offset);
        player.Play();

        var startedAt = await WaitUntilAudibleAsync(offset, cancellationToken);

        var startedAtTimestamp = timeProvider.GetTimestamp();
        var backstop = length + StallAllowance;
        TimeSpan heard;
        TimeSpan elapsed;

        while (true)
        {
            heard = player.Position - startedAt;
            elapsed = timeProvider.GetElapsedTime(startedAtTimestamp);

            if (heard >= length)
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

    private async Task<TimeSpan> WaitUntilAudibleAsync(TimeSpan offset, CancellationToken cancellationToken)
    {
        var startedAtTimestamp = timeProvider.GetTimestamp();

        while (true)
        {
            EnsureStillPlayable();

            var position = player.Position;

            if (player.State == PlaybackState.Playing && Difference(position, offset) <= SeekTolerance)
            {
                return position;
            }

            if (timeProvider.GetElapsedTime(startedAtTimestamp) > AudibleTimeout)
            {
                player.Pause();
                throw new SnippetPlaybackException("The player never reached the requested offset.");
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
