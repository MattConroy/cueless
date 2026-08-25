namespace Cueless.Domain;

public readonly record struct SnippetWindow
{
    private const double EarliestFraction = 0.05;
    private const double LatestFraction = 0.85;

    private SnippetWindow(TimeSpan earliest, TimeSpan latest)
    {
        Earliest = earliest;
        Latest = latest;
    }

    public TimeSpan Earliest { get; }

    public TimeSpan Latest { get; }

    public static SnippetWindow? Within(TimeSpan trackDuration, TimeSpan snippetLength)
    {
        var earliest = trackDuration * EarliestFraction;
        var latest = (trackDuration * LatestFraction) - snippetLength;

        return latest <= earliest ? null : new SnippetWindow(earliest, latest);
    }

    public TimeSpan OffsetAt(double position) =>
        position is < 0 or > 1
            ? throw new ArgumentOutOfRangeException(nameof(position), position, "Position must be between zero and one.")
            : Earliest + ((Latest - Earliest) * position);
}
