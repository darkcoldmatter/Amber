using Amber.Controls;

namespace Amber.Tests;

public class LevelSliderTests
{
    // Same geometry as the app: 384 px track, 9 px thumb, 0–100.
    static double ValueAt(double x) => LevelSlider.ValueAt(x, 384, 9, 0, 100);

    [Fact]
    public void Ends_of_the_track_give_minimum_and_maximum()
    {
        Assert.Equal(0, ValueAt(4.5));
        Assert.Equal(100, ValueAt(384 - 4.5));
    }

    [Fact]
    public void Middle_of_the_track_gives_the_middle_value()
    {
        Assert.Equal(50, ValueAt(192), 6);
    }

    [Fact]
    public void Pointer_outside_the_track_is_clamped()
    {
        Assert.Equal(0, ValueAt(-50));
        Assert.Equal(100, ValueAt(500));
    }
}
