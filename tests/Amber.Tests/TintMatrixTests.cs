using Amber.Tint;

namespace Amber.Tests;

public class TintMatrixTests
{
    static readonly TintMode[] Modes = Enum.GetValues<TintMode>();
    static readonly double[] Levels = [0, 1, 25, 50, 75, 99, 100];

    // What pure white turns into. All coefficients are non-negative, so no other color can come out brighter.
    static float[] White(float[] m) => [m[0] + m[5] + m[10], m[1] + m[6] + m[11], m[2] + m[7] + m[12]];

    [Fact]
    public void Deep_night_at_100_matches_the_tested_matrix()
    {
        float[] expected =
        [
            0.2126f, 0.05315f, 0.01063f, 0, 0,
            0.7152f, 0.17880f, 0.03576f, 0, 0,
            0.0722f, 0.01805f, 0.00361f, 0, 0,
            0, 0, 0, 1, 0,
            0, 0, 0, 0, 1,
        ];

        var actual = TintMatrix.Build(TintMode.DeepNight, 100);

        for (var k = 0; k < 25; k++) Assert.Equal(expected[k], actual[k], 0.0001f);
    }

    [Fact]
    public void A_fade_runs_from_an_untinted_screen_to_the_full_look()
    {
        var full = TintMatrix.Build(TintMode.Amber, 80);

        var identity = TintMatrix.Identity;
        var none = TintMatrix.Blend(TintMode.Amber, 80, 0);
        var all = TintMatrix.Blend(TintMode.Amber, 80, 1);
        var half = TintMatrix.Blend(TintMode.Amber, 80, 0.5);

        for (var k = 0; k < 25; k++)
        {
            Assert.Equal(identity[k], none[k], 0.0001f);
            Assert.Equal(full[k], all[k], 0.0001f);
            Assert.Equal((identity[k] + full[k]) / 2, half[k], 0.0001f);   // half way between the two
        }
    }

    [Fact]
    public void Even_the_faintest_look_fades_in_1_percent_steps_that_are_never_skipped()
    {
        foreach (var mode in Modes)
            foreach (var level in Levels)
                for (var step = 0; step < 100; step++)
                    Assert.True(TintMatrix.Differs(TintMatrix.Blend(mode, level, step / 100.0), TintMatrix.Blend(mode, level, (step + 1) / 100.0)),
                        $"{mode} at level {level}: the step to {step + 1} % would be skipped");
    }

    [Fact]
    public void Changes_too_small_to_see_are_skipped()
    {
        var almost = TintMatrix.Identity;
        almost[12] -= 0.0005f;   // an eighth of one 8-bit colour step, on blue

        Assert.False(TintMatrix.Differs(TintMatrix.Identity, TintMatrix.Identity));
        Assert.False(TintMatrix.Differs(TintMatrix.Identity, almost));
    }

    [Fact]
    public void White_never_blows_out()
    {
        foreach (var mode in Modes)
            foreach (var level in Levels)
                foreach (var channel in White(TintMatrix.Build(mode, level)))
                    Assert.InRange(channel, 0f, 1.0001f);
    }

    [Fact]
    public void Level_0_is_already_a_visible_tint()
    {
        foreach (var mode in Modes)
            Assert.True(White(TintMatrix.Build(mode, 0))[2] < 1f, $"{mode} at level 0 should reduce blue");
    }

    [Fact]
    public void Higher_level_never_lets_more_blue_through()
    {
        foreach (var mode in Modes)
        {
            var previous = float.MaxValue;
            foreach (var level in Levels)
            {
                var blue = White(TintMatrix.Build(mode, level))[2];
                Assert.True(blue <= previous, $"{mode}: blue went up at level {level}");
                previous = blue;
            }
        }
    }

    [Fact]
    public void Golden_hour_stays_softer_than_Amber_at_full_level()
    {
        var golden = White(TintMatrix.Build(TintMode.GoldenHour, 100));
        var amber = White(TintMatrix.Build(TintMode.Amber, 100));

        Assert.True(golden[1] > amber[1]);
        Assert.True(golden[2] > amber[2]);
    }

    [Fact]
    public void Level_outside_0_to_100_is_clamped()
    {
        foreach (var mode in Modes)
        {
            Assert.Equal(TintMatrix.Build(mode, 0), TintMatrix.Build(mode, -10));
            Assert.Equal(TintMatrix.Build(mode, 100), TintMatrix.Build(mode, 150));
        }
    }

    [Fact]
    public void Alpha_and_offset_are_left_alone()
    {
        var identity = TintMatrix.Identity;
        int[] untouched = [3, 4, 8, 9, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24];

        foreach (var mode in Modes)
        {
            var m = TintMatrix.Build(mode, 50);
            foreach (var k in untouched) Assert.Equal(identity[k], m[k]);
        }
    }

    [Theory]
    [InlineData(0, 1, 0, 0)]       // red
    [InlineData(30, 1, 0.5, 0)]    // orange, half way to yellow
    [InlineData(60, 1, 1, 0)]      // yellow
    [InlineData(120, 0, 1, 0)]     // green
    [InlineData(180, 0, 1, 1)]     // cyan
    [InlineData(240, 0, 0, 1)]     // blue
    [InlineData(300, 1, 0, 1)]     // magenta
    [InlineData(360, 1, 0, 0)]     // round to red again
    public void A_hue_is_the_full_colour_on_the_colour_bar(double hue, double r, double g, double b)
    {
        var colour = TintMatrix.HueColor(hue);

        Assert.Equal(r, colour.R, 6);
        Assert.Equal(g, colour.G, 6);
        Assert.Equal(b, colour.B, 6);
    }

    [Fact]
    public void Custom_at_100_turns_white_into_its_colour_and_greys_the_rest()
    {
        var m = TintMatrix.Build(TintMode.Custom, 100, 240);   // blue, lifted to the brightness floor

        Assert.Equal([0.35329f, 0.35329f, 1f], White(m), new FloatNear());
        // Mostly monochrome, as Amber at 100: red, green and blue all turn into shades of the colour.
        Assert.Equal(0.85 * 0.2126, m[0 * 5 + 2], 5);
        Assert.Equal(0.85 * 0.7152, m[1 * 5 + 2], 5);
        Assert.Equal(0.15 + 0.85 * 0.0722, m[2 * 5 + 2], 5);
    }

    [Fact]
    public void Custom_at_0_is_a_faint_wash_of_its_colour()
    {
        var m = TintMatrix.Build(TintMode.Custom, 0, 120);   // green

        Assert.Equal([0.85f, 1f, 0.85f], White(m), new FloatNear());
        // No mono: each channel only scales itself.
        for (var i = 0; i < 3; i++)
            for (var j = 0; j < 3; j++)
                if (i != j) Assert.Equal(0f, m[i * 5 + j]);
    }

    [Fact]
    public void Custom_fades_from_an_untinted_screen_to_its_look()
    {
        var full = TintMatrix.Build(TintMode.Custom, 60, 200);

        var identity = TintMatrix.Identity;
        var none = TintMatrix.Blend(TintMode.Custom, 60, 0, 200);
        var half = TintMatrix.Blend(TintMode.Custom, 60, 0.5, 200);
        var all = TintMatrix.Blend(TintMode.Custom, 60, 1, 200);

        for (var k = 0; k < 25; k++)
        {
            Assert.Equal(identity[k], none[k], 0.0001f);
            Assert.Equal((identity[k] + full[k]) / 2, half[k], 0.0001f);
            Assert.Equal(full[k], all[k], 0.0001f);
        }
    }

    [Fact]
    public void Custom_takes_the_colour_of_its_hue_and_the_other_tints_ignore_it()
    {
        Assert.NotEqual(TintMatrix.Build(TintMode.Custom, 50, 0), TintMatrix.Build(TintMode.Custom, 50, 200));
        Assert.Equal(TintMatrix.Build(TintMode.Custom, 50, 0), TintMatrix.Build(TintMode.Custom, 50, 360));

        foreach (var mode in new[] { TintMode.GoldenHour, TintMode.Amber, TintMode.DeepNight })
            Assert.Equal(TintMatrix.Build(mode, 50), TintMatrix.Build(mode, 50, 200));
    }

    [Fact]
    public void Custom_never_blows_out_whatever_the_hue()
    {
        for (var hue = 0; hue <= 360; hue += 15)
            foreach (var level in Levels)
                foreach (var channel in White(TintMatrix.Build(TintMode.Custom, level, hue)))
                    Assert.InRange(channel, 0f, 1.0001f);
    }

    // Custom's brightness floor (TintMatrix.CustomMinBrightness), by the same Rec.709 luma the looks use.
    const double Floor = 0.40;
    static double Brightness((double R, double G, double B) c) => 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;

    [Fact]
    public void No_custom_colour_is_darker_than_the_floor_and_dark_hues_reach_it_exactly()
    {
        for (var hue = 0; hue < 360; hue++)
        {
            var pure = TintMatrix.HueColor(hue);
            var lifted = TintMatrix.CustomColor(hue);
            Assert.True(Brightness(lifted) >= Floor - 1e-9, $"hue {hue} is too dark");
            if (Brightness(pure) < Floor) Assert.Equal(Floor, Brightness(lifted), 9);
        }
    }

    [Theory]
    [InlineData(60)]    // yellow
    [InlineData(120)]   // green
    [InlineData(180)]   // cyan
    [InlineData(30)]    // orange
    public void Bright_hues_keep_their_pure_colour(double hue)
    {
        Assert.Equal(TintMatrix.HueColor(hue), TintMatrix.CustomColor(hue));
    }

    [Theory]
    [InlineData(240, 0.3533, 0.3533, 1)]   // blue: pure blue keeps only about 7 % of white's brightness
    [InlineData(0, 1, 0.2380, 0.2380)]     // red
    [InlineData(300, 1, 0.1611, 1)]        // magenta
    public void Dark_hues_get_just_enough_white(double hue, double r, double g, double b)
    {
        var colour = TintMatrix.CustomColor(hue);

        Assert.Equal(r, colour.R, 3);
        Assert.Equal(g, colour.G, 3);
        Assert.Equal(b, colour.B, 3);
    }

    [Fact]
    public void The_lifted_colour_changes_smoothly_round_the_whole_bar()
    {
        var previous = TintMatrix.CustomColor(0);
        for (var tenths = 1; tenths <= 3600; tenths++)   // in 0.1° steps, back round to 360 = 0
        {
            var colour = TintMatrix.CustomColor(tenths / 10.0);
            var step = Math.Max(Math.Abs(colour.R - previous.R), Math.Max(Math.Abs(colour.G - previous.G), Math.Abs(colour.B - previous.B)));
            Assert.True(step < 0.005, $"jump of {step:F4} at {tenths / 10.0}°");
            previous = colour;
        }
    }

    [Fact]
    public void Custom_at_100_turns_white_into_the_lifted_colour()
    {
        for (var hue = 0; hue < 360; hue += 15)
        {
            var (r, g, b) = TintMatrix.CustomColor(hue);
            Assert.Equal([(float)r, (float)g, (float)b], White(TintMatrix.Build(TintMode.Custom, 100, hue)), new FloatNear());
        }
    }

    sealed class FloatNear : IEqualityComparer<float>
    {
        public bool Equals(float a, float b) => Math.Abs(a - b) < 0.0001f;
        public int GetHashCode(float value) => 0;
    }
}
