namespace Cueless.Application.Playback;

public sealed record PlaybackSettings
{
    // Pausing crosses the frame boundary, so the snippet stops this much early to
    // absorb the time the message spends in flight.
    public TimeSpan PauseLead { get; init; } = TimeSpan.FromMilliseconds(20);
}
