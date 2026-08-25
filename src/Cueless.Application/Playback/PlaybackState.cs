namespace Cueless.Application.Playback;

public enum PlaybackState
{
    Unstarted,
    Ended,
    Playing,
    Paused,
    Buffering,
    Cued,
    Unavailable,
}
