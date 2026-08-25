using Cueless.Application.Playback;

namespace Cueless.Application.UnitTests;

internal sealed class FakeMediaPlayer : IMediaPlayer
{
    private readonly Queue<TimeSpan> readings = new();
    private TimeSpan latest;

    public PlaybackState State { get; set; } = PlaybackState.Playing;

    public bool Paused { get; private set; }

    public string? CuedVideoIdentifier { get; private set; }

    public TimeSpan? SoughtTo { get; private set; }

    public bool Started { get; private set; }

    public TimeSpan Position
    {
        get
        {
            if (readings.Count > 0)
            {
                latest = readings.Dequeue();
            }

            return latest;
        }
    }

    public void Reports(params TimeSpan[] positions)
    {
        foreach (var position in positions)
        {
            readings.Enqueue(position);
        }
    }

    public void Cue(string videoIdentifier) => CuedVideoIdentifier = videoIdentifier;

    public void Seek(TimeSpan position) => latest = position;

    public void Play() => Started = true;

    public void Pause() => Paused = true;
}
