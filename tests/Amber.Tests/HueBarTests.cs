using Amber.Controls;
using System.Windows.Media;

namespace Amber.Tests;

public class HueBarTests
{
    // Same geometry as the app: a 400 px bar, a 42 px handle, hues 0–360.
    static double HueAt(double x) => LevelSlider.ValueAt(x, 400, 42, 0, 360);
    static bool OnHandle(double x, double hue) => HueBar.IsOnHandle(x, hue, 400, 42, 0, 360);

    [Fact]
    public void The_handle_centre_is_the_hue()
    {
        Assert.Equal(0, HueAt(21));
        Assert.Equal(180, HueAt(200), 6);
        Assert.Equal(360, HueAt(379));
        Assert.Equal(0, HueAt(-30));
        Assert.Equal(360, HueAt(430));
    }

    [Fact]
    public void The_whole_handle_is_the_grip()
    {
        Assert.True(OnHandle(0, 0));
        Assert.True(OnHandle(42, 0));
        Assert.False(OnHandle(43, 0));
        Assert.True(OnHandle(179, 180));
        Assert.True(OnHandle(221, 180));
        Assert.False(OnHandle(178, 180));
        Assert.True(OnHandle(400, 360));
        Assert.False(OnHandle(357, 360));
    }

    [Fact]
    public void The_handle_is_filled_with_the_hue_colour()
    {
        Assert.Equal(Color.FromRgb(255, 0, 0), HueBar.ColorOf(0));
        Assert.Equal(Color.FromRgb(255, 128, 0), HueBar.ColorOf(30));
        Assert.Equal(Color.FromRgb(0, 0, 255), HueBar.ColorOf(240));
        Assert.Equal(Color.FromRgb(255, 0, 0), HueBar.ColorOf(360));
    }
}
