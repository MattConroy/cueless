namespace Cueless.Application.Playback;

public sealed record SnippetProgress(string Phase, PlaybackState State, TimeSpan Position, TimeSpan Elapsed);
