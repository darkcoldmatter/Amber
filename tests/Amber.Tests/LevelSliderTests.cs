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

    static bool OnGrip(double x, double value) => LevelSlider.IsOnGrip(x, value, 384, 9, 0, 100);

    [Fact]
    public void The_grip_reaches_from_6_px_before_its_line_to_3_px_past_the_fill()
    {
        // At 50 the thumb's middle, the grip line, is at x 192 (the value a press there gives); the fill ends at its right
        // edge, 196.5.
        Assert.Equal(50, ValueAt(192), 6);
        Assert.True(OnGrip(192, 50));
        Assert.True(OnGrip(186, 50));
        Assert.True(OnGrip(196.5, 50));
        Assert.True(OnGrip(199.5, 50));
        Assert.False(OnGrip(185, 50));
        Assert.False(OnGrip(200, 50));
    }

    [Fact]
    public void The_grip_follows_the_value_to_both_ends()
    {
        Assert.True(OnGrip(4.5, 0));
        Assert.False(OnGrip(192, 0));
        Assert.True(OnGrip(384 - 4.5, 100));
        Assert.True(OnGrip(384 + 3, 100));
        Assert.False(OnGrip(384 - 4.5 - 7, 100));
    }
}
