using Amber.Tint;

namespace Amber.Tests;

public class AdvancedScheduleTests
{
    // How often the app looks at the clock while nothing is fading.
    static readonly TimeSpan IdleCheck = TimeSpan.FromSeconds(5);

    static Filter Between(TimeOnly start, TimeOnly end, TintMode mode = TintMode.Amber, double level = 50) =>
        new() { Mode = mode, Start = start, End = end, Level = level };

    static TimeOnly Time(int hour, int minute = 0) => new(hour, minute);

    // Some seconds before (negative) or after a moment, wrapping round midnight.
    static TimeOnly Around(TimeOnly time, double seconds) => time.Add(TimeSpan.FromSeconds(seconds));

    // How strongly the filters show at a moment, all together.
    static double Total(IEnumerable<Filter> filters, TimeOnly now) => AdvancedSchedule.Weights(filters, now).Sum(s => s.Weight);

    static double WeightOf(Filter filter, TimeOnly now) => Total([filter], now);

    // What the screen shows, mixed the way the app mixes it.
    static float[] Screen(IEnumerable<Filter> filters, TimeOnly now) =>
        TintMatrix.Mix(AdvancedSchedule.Weights(filters, now)
            .Select(s => (TintMatrix.Build(s.Filter.Mode, AdvancedSchedule.LevelAt(s.Filter, now)), s.Weight)));

    // How much blue pure white keeps: 1 on a clear screen, less under a stronger tint.
    static float Blue(float[] m) => m[2] + m[7] + m[12];

    [Fact]
    public void Inside_its_hours_a_filter_is_fully_on_and_away_from_them_it_is_off()
    {
        var filter = Between(Time(8), Time(16));

        Assert.Equal(1, WeightOf(filter, Time(8, 1)));
        Assert.Equal(1, WeightOf(filter, Time(12)));
        Assert.Equal(1, WeightOf(filter, Time(15, 59)));
        Assert.Equal(0, WeightOf(filter, Time(7, 59)));
        Assert.Equal(0, WeightOf(filter, Time(16, 1)));
        Assert.Equal(0, WeightOf(filter, Time(23)));
    }

    [Fact]
    public void A_filter_fades_in_over_10_seconds_centred_on_its_start()
    {
        var filter = Between(Time(8), Time(16));

        Assert.Equal(0, WeightOf(filter, Around(Time(8), -5)), 3);
        Assert.Equal(0.25, WeightOf(filter, Around(Time(8), -2.5)), 3);
        Assert.Equal(0.5, WeightOf(filter, Time(8)), 3);
        Assert.Equal(0.75, WeightOf(filter, Around(Time(8), 2.5)), 3);
        Assert.Equal(1, WeightOf(filter, Around(Time(8), 5)), 3);
    }

    [Fact]
    public void It_fades_out_the_same_way_centred_on_its_end()
    {
        var filter = Between(Time(8), Time(16));

        Assert.Equal(1, WeightOf(filter, Around(Time(16), -5)), 3);
        Assert.Equal(0.75, WeightOf(filter, Around(Time(16), -2.5)), 3);
        Assert.Equal(0.5, WeightOf(filter, Time(16)), 3);
        Assert.Equal(0.25, WeightOf(filter, Around(Time(16), 2.5)), 3);
        Assert.Equal(0, WeightOf(filter, Around(Time(16), 5)), 3);
    }

    [Fact]
    public void Touching_filters_cross_fade_with_weights_that_always_add_up_to_1()
    {
        var (first, second) = (Between(Time(8), Time(9)), Between(Time(9), Time(10)));

        // Every tenth of a second (the app's step during a fade), from 8 s before the edge to 8 s after.
        for (var tenths = -80; tenths <= 80; tenths++)
            Assert.Equal(1, Total([first, second], Around(Time(9), tenths / 10.0)), 9);

        Assert.Equal(0.5, WeightOf(first, Time(9)), 3);   // on the edge itself, half each
        Assert.Equal(0.5, WeightOf(second, Time(9)), 3);
    }

    [Fact]
    public void A_cross_fade_never_falls_back_toward_a_clear_screen()
    {
        // Golden hour handing over to Deep night: the screen never keeps more blue than the bluer of the two alone.
        Filter[] filters = [Between(Time(8), Time(9), TintMode.GoldenHour, 100), Between(Time(9), Time(10), TintMode.DeepNight, 100)];
        var bluer = Math.Max(Blue(Screen(filters, Time(8, 30))), Blue(Screen(filters, Time(9, 30))));

        for (var tenths = -80; tenths <= 80; tenths++)
        {
            var now = Around(Time(9), tenths / 10.0);
            Assert.True(Blue(Screen(filters, now)) <= bluer + 0.0001f, $"more blue at {now:HH:mm:ss.f}");
        }
    }

    [Fact]
    public void A_15_minute_gap_fades_out_to_a_clear_screen_and_back_in()
    {
        Filter[] filters = [Between(Time(8), Time(9)), Between(Time(9, 15), Time(10))];

        Assert.Equal(0.5, Total(filters, Time(9)), 3);   // the first one halfway out
        for (var second = 5; second <= 15 * 60 - 5; second++)   // from the end of its fade to the start of the next one
            Assert.Empty(AdvancedSchedule.Weights(filters, Around(Time(9), second)));
        Assert.Equal(0.5, Total(filters, Time(9, 15)), 3);   // the second one halfway in
    }

    [Fact]
    public void Filters_touching_at_midnight_cross_fade_across_it()
    {
        var (evening, night) = (Between(Time(22), Time(0)), Between(Time(0), Time(2)));

        for (var tenths = -80; tenths <= 80; tenths++)
            Assert.Equal(1, Total([evening, night], Around(Time(0), tenths / 10.0)), 9);

        Assert.Equal(0.8, WeightOf(evening, Around(Time(0), -3)), 3);   // 23:59:57: still mostly the evening filter
        Assert.Equal(0.2, WeightOf(night, Around(Time(0), -3)), 3);
        Assert.Equal(0.5, WeightOf(night, Time(0)), 3);
        Assert.Equal(1, WeightOf(night, Around(Time(0), 5)), 3);
    }

    [Fact]
    public void A_whole_day_filter_has_no_edges_and_is_always_fully_on()
    {
        var filter = Between(Time(8), Time(8));

        double[] moments = [-5, -2.5, 0, 2.5, 5, 12 * 60 * 60];
        foreach (var seconds in moments) Assert.Equal(1, WeightOf(filter, Around(Time(8), seconds)));
        Assert.False(AdvancedSchedule.IsFading([filter], Around(Time(8), -7), IdleCheck));
    }

    [Fact]
    public void A_curve_filter_fades_in_at_its_start_level_and_out_at_its_end_level()
    {
        var filter = Between(Time(8), Time(9));
        filter.Curve = [new(0, 20), new(60, 90)];
        filter.CurveEnabled = true;

        Assert.Equal(20, AdvancedSchedule.LevelAt(filter, Around(Time(8), -3)));   // not the end's 90: the start is nearer
        Assert.Equal(20, AdvancedSchedule.LevelAt(filter, Time(8)));
        Assert.Equal(90, AdvancedSchedule.LevelAt(filter, Time(9)));
        Assert.Equal(90, AdvancedSchedule.LevelAt(filter, Around(Time(9), 3)));
    }

    [Fact]
    public void Fading_is_reported_from_one_idle_check_before_a_fade_until_it_ends()
    {
        Filter[] filters = [Between(Time(8), Time(9))];
        bool FadingAt(TimeOnly now) => AdvancedSchedule.IsFading(filters, now, IdleCheck);

        Assert.False(FadingAt(Time(7, 30)));
        Assert.False(FadingAt(Around(Time(8), -10.1)));   // the next idle check still comes before the fade begins at -5 s
        Assert.True(FadingAt(Around(Time(8), -10)));      // it would come too late, so start checking often now
        Assert.True(FadingAt(Time(8)));
        Assert.True(FadingAt(Around(Time(8), 4.9)));
        Assert.False(FadingAt(Around(Time(8), 5)));       // fully on
        Assert.False(FadingAt(Time(8, 30)));
        Assert.True(FadingAt(Around(Time(9), -10)));      // the same before the fade-out
        Assert.False(FadingAt(Around(Time(9), 5)));
    }

    [Fact]
    public void Checking_every_5_seconds_never_notices_a_fade_late()
    {
        // However the idle checks fall against the clock, the app starts checking often before the fade-in begins.
        Filter[] filters = [Between(Time(8), Time(9))];
        var fadeBegins = Around(Time(8), -5);

        for (var tenths = 0; tenths < 50; tenths++)
        {
            var check = Around(Time(7, 58), tenths / 10.0);
            while (!AdvancedSchedule.IsFading(filters, check, IdleCheck)) check = check.Add(IdleCheck);
            Assert.True(check <= fadeBegins, $"first noticed at {check:HH:mm:ss.f}");
        }
    }
}
