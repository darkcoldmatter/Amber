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

    // What a colour turns into, without the screen's clipping: for comparing two matrices that should be the same.
    static (double R, double G, double B) Raw(float[] m, double r, double g, double b)
    {
        double Channel(int j) => r * m[j] + g * m[5 + j] + b * m[10 + j] + m[20 + j];
        return (Channel(0), Channel(1), Channel(2));
    }

    static void Near((double R, double G, double B) expected, (double R, double G, double B) actual, double tolerance = 0.002)
    {
        Assert.Equal(expected.R, actual.R, tolerance);
        Assert.Equal(expected.G, actual.G, tolerance);
        Assert.Equal(expected.B, actual.B, tolerance);
    }

    static (double, double, double) Grey(double v) => (v, v, v);

    [Fact]
    public void E_ink_lifts_black_to_charcoal_and_dims_white_to_paper_grey()
    {
        // The owner's picks: black 70 % and white 25 % on the experiment's test sliders.
        Assert.Equal(0.105, TintMatrix.EInkInk);
        Assert.Equal(0.925, TintMatrix.EInkPaper);

        var m = TintMatrix.EInk(0, classic: false);
        Near(Grey(0.105), Show(m, 0, 0, 0));
        Near(Grey(0.925), Show(m, 1, 1, 1));
        Near(Grey(0.515), Show(m, 0.5, 0.5, 0.5));   // halfway between them
    }

    [Fact]
    public void Classic_is_plain_greyscale_from_black_to_white()
    {
        var m = TintMatrix.EInk(0, classic: true);
        Near(Grey(0), Show(m, 0, 0, 0));
        Near(Grey(1), Show(m, 1, 1, 1));
        Near(Grey(0.5), Show(m, 0.5, 0.5, 0.5));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Colours_of_the_same_brightness_become_the_same_grey(bool classic)
    {
        var m = TintMatrix.EInk(0, classic);
        // Pure blue and a dark grey of the same Rec.709 luma (0.0722) look the same; so do red and its grey.
        Near(Show(m, 0.0722, 0.0722, 0.0722), Show(m, 0, 0, 1));
        Near(Show(m, 0.2126, 0.2126, 0.2126), Show(m, 1, 0, 0));
        // No colour is left.
        var green = Show(m, 0, 1, 0);
        Assert.Equal(green.R, green.G, 6);
        Assert.Equal(green.G, green.B, 6);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Level_0_is_the_plain_grey_look(bool classic)
    {
        // No amber at all: a colour shows as ink + (paper − ink) × its luma, the same in every channel.
        var (ink, paper) = classic ? (0.0, 1.0) : (TintMatrix.EInkInk, TintMatrix.EInkPaper);
        var luma = 0.2126 * 0.2 + 0.7152 * 0.7 + 0.0722 * 0.4;
        Near(Grey(ink + (paper - ink) * luma), Show(TintMatrix.EInk(0, classic), 0.2, 0.7, 0.4), 0.0001);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(35)]
    [InlineData(100)]
    public void The_level_is_amber_over_the_grey_look(double level)
    {
        foreach (var classic in new[] { false, true })
        {
            var grey = TintMatrix.EInk(0, classic);
            var amber = TintMatrix.Blend(TintMode.Amber, 100, level / 100);
            var m = TintMatrix.EInk(level, classic);
            foreach (var (r, g, b) in new[] { (0.0, 0.0, 0.0), (1.0, 1.0, 1.0), (0.2, 0.7, 0.4), (0.0, 0.0, 1.0) })
            {
                var paper = Raw(grey, r, g, b);
                Near(Raw(amber, paper.R, paper.G, paper.B), Raw(m, r, g, b), 0.0001);
            }
        }
    }

    [Fact]
    public void Level_100_turns_the_paper_amber()
    {
        // Amber's level-100 colour is (1, 0.45, 0.02), and on a grey input its mono part changes nothing.
        Near((0.925, 0.925 * 0.45, 0.925 * 0.02), Show(TintMatrix.EInk(100, classic: false), 1, 1, 1));
        Near((1, 0.45, 0.02), Show(TintMatrix.EInk(100, classic: true), 1, 1, 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void More_level_means_less_blue(bool classic)
    {
        var before = double.MaxValue;
        for (var level = 0; level <= 100; level += 10)
        {
            var blue = Show(TintMatrix.EInk(level, classic), 1, 1, 1).B;
            Assert.True(blue < before, $"blue {blue} at level {level} should be below {before}");
            before = blue;
        }
    }

    [Fact]
    public void The_level_is_kept_within_0_to_100()
    {
        Assert.Equal(TintMatrix.EInk(0, false), TintMatrix.EInk(-20, false));
        Assert.Equal(TintMatrix.EInk(100, true), TintMatrix.EInk(130, true));
    }

    [Fact]
    public void Transparency_is_left_alone()
    {
        foreach (var classic in new[] { false, true })
            foreach (var level in new[] { 0, 30, 60, 100 })
            {
                var m = TintMatrix.EInk(level, classic);
                Assert.Equal([0f, 0f, 0f, 1f, 0f], m[15..20]);                       // alpha in: only alpha out
                Assert.Equal([0f, 0f, 0f, 0f], new[] { m[3], m[8], m[13], m[23] });   // alpha out: only from alpha
                Assert.Equal(1, m[24]);
            }
    }

    [Fact]
    public void Multiplying_by_identity_changes_nothing()
    {
        var m = TintMatrix.EInk(40, classic: false);
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
}
