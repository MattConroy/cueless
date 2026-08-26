using Cueless.Domain;

namespace Cueless.Domain.UnitTests;

public class SnippetWindowTests
{
    [Fact]
    public void WindowStartsAfterTheIntroduction()
    {
        var window = SnippetWindow.Within(TimeSpan.FromMinutes(4), TimeSpan.FromSeconds(1));

        Assert.NotNull(window);
        Assert.Equal(TimeSpan.FromSeconds(12), window.Value.Earliest);
    }

    [Fact]
    public void WindowEndsBeforeTheFadeOutAndLeavesRoomForTheSnippet()
    {
        var window = SnippetWindow.Within(TimeSpan.FromMinutes(4), TimeSpan.FromSeconds(3));

        Assert.NotNull(window);
        Assert.Equal(TimeSpan.FromSeconds(201), window.Value.Latest);
    }

    [Fact]
    public void LongerSnippetsNarrowTheWindow()
    {
        var duration = TimeSpan.FromMinutes(4);

        var shortest = SnippetWindow.Within(duration, TimeSpan.FromSeconds(1));
        var longest = SnippetWindow.Within(duration, TimeSpan.FromSeconds(3));

        Assert.True(longest!.Value.Latest < shortest!.Value.Latest);
        Assert.Equal(shortest.Value.Earliest, longest.Value.Earliest);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(1)]
    public void OffsetStaysInsideTheWindow(double position)
    {
        var window = SnippetWindow.Within(TimeSpan.FromMinutes(4), TimeSpan.FromSeconds(1))!.Value;

        var offset = window.OffsetAt(position);

        Assert.InRange(offset, window.Earliest, window.Latest);
    }

    [Fact]
    public void TrackTooShortForTheSnippetHasNoWindow() =>
        Assert.Null(SnippetWindow.Within(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3)));

    [Fact]
    public void PositionOutsideZeroToOneIsRejected()
    {
        var window = SnippetWindow.Within(TimeSpan.FromMinutes(4), TimeSpan.FromSeconds(1))!.Value;

        Assert.Throws<ArgumentOutOfRangeException>(() => window.OffsetAt(1.5));
    }
}
