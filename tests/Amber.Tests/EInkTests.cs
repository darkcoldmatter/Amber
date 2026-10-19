using Amber.Tint;

namespace Amber.Tests;

public class EInkTests
{
    // What a colour (r, g, b, 0–1) turns into, clipped to 0–1 like the screen does.
    static (double R, double G, double B) Show(float[] m, double r, double g, double b)
    {
        double Channel(int j) => Math.Clamp(r * m[j] + g * m[5 + j] + b * m[10 + j] + m[20 + j], 0, 1);
        return (Channel(0), Channel(1), Channel(2));
    }

    static void Near((double R, double G, double B) expected, (double R, double G, double B) actual, double tolerance = 0.002)
    {
        Assert.Equal(expected.R, actual.R, tolerance);
        Assert.Equal(expected.G, actual.G, tolerance);
        Assert.Equal(expected.B, actual.B, tolerance);
    }

    [Fact]
    public void The_soft_look_lifts_black_to_charcoal_and_pulls_white_down_to_paper()
    {
        var m = TintMatrix.EInk(EInkLook.Soft);
        Near(EInkLook.Soft.Ink, Show(m, 0, 0, 0));
        Near(EInkLook.Soft.Paper, Show(m, 1, 1, 1));
        Near((0.515, 0.51, 0.49), Show(m, 0.5, 0.5, 0.5));   // halfway between ink and paper
    }

    [Fact]
    public void The_crisp_look_has_more_contrast_but_still_no_pure_black()
    {
        var m = TintMatrix.EInk(EInkLook.Crisp);
        Near(EInkLook.Crisp.Ink, Show(m, 0.05, 0.05, 0.05));     // 5 % grey is the ink
        Near(EInkLook.Crisp.Paper, Show(m, 0.95, 0.95, 0.95));   // 95 % grey is the paper
        var black = Show(m, 0, 0, 0);
        Assert.InRange(black.R, 0.06, 0.08);                    // a touch darker than the ink, still not black
        var white = Show(m, 1, 1, 1);
        Assert.True(white.R >= EInkLook.Crisp.Paper.R && white.B < 0.9, "white is paper or a touch lighter, still cream");
    }

    [Fact]
    public void Colours_of_the_same_brightness_become_the_same_grey()
    {
        foreach (var look in EInkLook.All)
        {
            var m = TintMatrix.EInk(look);
            // Pure blue and a dark grey of the same Rec.709 luma (0.0722) look the same.
            Near(Show(m, 0.0722, 0.0722, 0.0722), Show(m, 0, 0, 1));
            // Every colour comes out on the line from ink to paper: no colour left, only the paper's warmth.
            var red = Show(m, 1, 0, 0);
            var grey = Show(m, 0.2126, 0.2126, 0.2126);
            Near(grey, red);
        }
    }

    [Fact]
    public void Transparency_is_left_alone()
    {
        foreach (var look in EInkLook.All)
        {
            var m = TintMatrix.EInk(look);
            Assert.Equal(1, m[18]);
            Assert.Equal(1, m[24]);
            Assert.Equal(0, m[3]);
            Assert.Equal(0, m[23]);
        }
    }
}
