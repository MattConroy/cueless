namespace Cueless.Application.Playback;

public interface IMediaPlayer
{
    PlaybackState State { get; }

    TimeSpan Position { get; }

    void Cue(string videoIdentifier, TimeSpan startAt);

    void Seek(TimeSpan position);

    void Play();

    void Pause();
}
