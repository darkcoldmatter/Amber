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
}
