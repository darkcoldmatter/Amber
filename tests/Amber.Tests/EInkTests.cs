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

    // What a colour turns into, without the screen's clipping: for comparing two matrices that should be the same.
    static (double R, double G, double B) Raw(float[] m, double r, double g, double b)
    {
        double Channel(int j) => r * m[j] + g * m[5 + j] + b * m[10 + j] + m[20 + j];
        return (Channel(0), Channel(1), Channel(2));
    }

    [Fact]
    public void Multiplying_by_identity_changes_nothing()
    {
        var m = TintMatrix.EInk(EInkLook.Crisp);
        Assert.Equal(m, TintMatrix.Multiply(m, TintMatrix.Identity));
        Assert.Equal(m, TintMatrix.Multiply(TintMatrix.Identity, m));
    }

    [Fact]
    public void Multiply_applies_the_first_matrix_then_the_second()
    {
        var swap = TintMatrix.Identity;          // red and green trade places
        swap[0] = 0; swap[1] = 1; swap[5] = 1; swap[6] = 0;
        var halveRed = TintMatrix.Identity;
        halveRed[0] = 0.5f;
        var addRed = TintMatrix.Identity;        // the offset row: + 0.1 red
        addRed[20] = 0.1f;
        var doubleRed = TintMatrix.Identity;
        doubleRed[0] = 2;

        Near((0.3, 0.8, 0.5), Raw(TintMatrix.Multiply(swap, halveRed), 0.8, 0.6, 0.5));   // swapped, then the new red halved
        Near((0.6, 0.4, 0.5), Raw(TintMatrix.Multiply(halveRed, swap), 0.8, 0.6, 0.5));   // halved, then swapped
        Near((1.2, 0.6, 0.5), Raw(TintMatrix.Multiply(addRed, doubleRed), 0.5, 0.6, 0.5)); // (0.5 + 0.1) × 2
        Near((1.1, 0.6, 0.5), Raw(TintMatrix.Multiply(doubleRed, addRed), 0.5, 0.6, 0.5)); // 0.5 × 2 + 0.1
    }

    [Fact]
    public void No_amber_is_the_plain_e_ink_look()
    {
        foreach (var look in EInkLook.All)
            Assert.Equal(TintMatrix.EInk(look), TintMatrix.EInk(look, 0));
    }

    [Fact]
    public void Amber_goes_on_top_of_the_e_ink_look()
    {
        var amber = TintMatrix.Build(TintMode.Amber, 100);
        foreach (var look in EInkLook.All)
        {
            var m = TintMatrix.EInk(look, 100);
            foreach (var (r, g, b) in new[] { (0.0, 0.0, 0.0), (1.0, 1.0, 1.0), (0.2, 0.7, 0.4), (0.0, 0.0, 1.0) })
            {
                var paper = Raw(TintMatrix.EInk(look), r, g, b);
                Near(Raw(amber, paper.R, paper.G, paper.B), Raw(m, r, g, b), 0.0001);
            }
        }
    }

    [Fact]
    public void Full_amber_turns_the_paper_amber()
    {
        // Amber's level-100 colour is (1, 0.45, 0.02). It is 85 % monochrome, so the slightly warm paper comes out a touch
        // off the plain product: hence the tolerance.
        foreach (var look in EInkLook.All)
        {
            var paperGrey = look.High;   // the brightness that shows as paper
            var shown = Show(TintMatrix.EInk(look, 100), paperGrey, paperGrey, paperGrey);
            Near((look.Paper.R * 1.00, look.Paper.G * 0.45, look.Paper.B * 0.02), shown, 0.03);
        }
    }

    [Fact]
    public void More_amber_means_less_blue()
    {
        foreach (var look in EInkLook.All)
        {
            var before = double.MaxValue;
            for (var amber = 0; amber <= 100; amber += 10)
            {
                var blue = Show(TintMatrix.EInk(look, amber), 1, 1, 1).B;
                Assert.True(blue < before, $"{look.Name}: blue {blue} at amber {amber} should be below {before}");
                before = blue;
            }
        }
    }

    [Fact]
    public void Amber_leaves_transparency_alone()
    {
        foreach (var look in EInkLook.All)
            foreach (var amber in new[] { 0, 30, 60, 100 })
            {
                var m = TintMatrix.EInk(look, amber);
                Assert.Equal([0f, 0f, 0f, 1f, 0f], m[15..20]);           // alpha in: only alpha out
                Assert.Equal([0f, 0f, 0f, 0f], new[] { m[3], m[8], m[13], m[23] });   // alpha out: only from alpha
                Assert.Equal(1, m[24]);
            }
    }
}
