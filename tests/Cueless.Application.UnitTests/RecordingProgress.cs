using Cueless.Application.Playback;

namespace Cueless.Application.UnitTests;

internal sealed class RecordingProgress : IProgress<SnippetProgress>
{
    public List<SnippetProgress> Samples { get; } = [];

    public void Report(SnippetProgress value) => Samples.Add(value);
}
