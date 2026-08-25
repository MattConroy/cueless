namespace Cueless.Application.Playback;

public sealed record SnippetOutcome(
    TimeSpan Requested,
    TimeSpan Heard,
    TimeSpan Elapsed,
    TimeSpan StartedAt)
{
    public TimeSpan Shortfall => Requested - Heard;

    public TimeSpan Stalled => Elapsed - Heard;
}
