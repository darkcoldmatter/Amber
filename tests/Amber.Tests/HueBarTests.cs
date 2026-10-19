using Amber.Controls;
using System.Windows.Media;

namespace Amber.Tests;

public class HueBarTests
{
    // Same geometry as the app: a 400 px bar whose track is 2 px inside it (396 px), an 18 px handle, hues 0–360.
    static double HueAt(double x) => LevelSlider.ValueAt(x, 396, 18, 0, 360);
    static bool OnHandle(double x, double hue) => HueBar.IsOnHandle(x, hue, 396, 18, 0, 360);
    static (double Start, double End) Ends(double hue) => HueBar.EndOpacities(hue, 396, 18, 0, 360);

    [Fact]
    public void The_handle_centre_is_the_hue()
    {
        Assert.Equal(0, HueAt(9));
        Assert.Equal(180, HueAt(198), 6);
        Assert.Equal(360, HueAt(387));
        Assert.Equal(0, HueAt(-30));
        Assert.Equal(360, HueAt(430));
    }

    [Fact]
    public void The_whole_handle_is_the_grip()
    {
        Assert.True(OnHandle(0, 0));
        Assert.True(OnHandle(18, 0));
        Assert.False(OnHandle(19, 0));
        Assert.True(OnHandle(189, 180));
        Assert.True(OnHandle(207, 180));
        Assert.False(OnHandle(188, 180));
        Assert.True(OnHandle(396, 360));
        Assert.False(OnHandle(377, 360));
    }

    [Fact]
    public void The_handle_shows_what_white_turns_into()
    {
        Assert.Equal(Color.FromRgb(255, 61, 61), HueBar.ColorOf(0));     // red, lifted to the brightness floor
        Assert.Equal(Color.FromRgb(255, 128, 0), HueBar.ColorOf(30));    // orange is bright enough as it is
        Assert.Equal(Color.FromRgb(90, 90, 255), HueBar.ColorOf(240));   // blue, lifted
        Assert.Equal(HueBar.ColorOf(0), HueBar.ColorOf(360));
    }

    [Fact]
    public void The_rainbow_end_under_the_handle_hides_while_the_handle_covers_it()
    {
        // 378 px of travel for 360 hues: 1 px is 360 / 378 of a hue.
        Assert.Equal((0, 1), Ends(0));
        Assert.Equal((1, 0), Ends(360));
        Assert.Equal((1, 1), Ends(180));
        Assert.Equal(0.25, Ends(360.0 / 378).Start, 6);  // 1 px away: a quarter shown
        Assert.Equal(1, Ends(4 * 360.0 / 378).Start, 6);   // 4 px away (the handle's border): all of it
        Assert.Equal(0.2625, Ends(359).End, 6);            // 1.05 px from the end
    }
}
