namespace Cueless.Application.Playback;

public sealed record SnippetOutcome(
    TimeSpan Requested,
    TimeSpan Heard,
    TimeSpan Delivered,
    TimeSpan Elapsed,
    TimeSpan StartedAt)
{
    public TimeSpan Shortfall => Requested - Heard;

    // Pausing crosses the frame boundary, so audio keeps playing until the player acts on it.
    public TimeSpan Overshoot => Delivered - Requested;

    public TimeSpan Stalled => Elapsed - Heard;
}
