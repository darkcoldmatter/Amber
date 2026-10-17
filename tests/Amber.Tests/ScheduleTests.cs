namespace Amber.Tests;

public class ScheduleTests
{
    static readonly TimeOnly Start = new(20, 0), End = new(8, 0);   // overnight, 12 hours
    static readonly TimeSpan IdleCheck = TimeSpan.FromSeconds(5);   // how often the app looks while nothing fades

    static double StrengthAt(int hour, int minute = 0, int transition = 30 * 60) =>
        Schedule.StrengthAt(new TimeOnly(hour, minute), Start, End, transition);

    static bool IsFading(TimeOnly now, int transition = 30 * 60) => Schedule.IsFading(now, Start, End, transition, IdleCheck);

    [Fact]
    public void Outside_the_hours_there_is_no_tint()
    {
        Assert.Equal(0, StrengthAt(12));
        Assert.Equal(0, StrengthAt(19, 59));
        Assert.Equal(0, StrengthAt(8, 1));
    }

    [Fact]
    public void Inside_the_hours_the_tint_is_full()
    {
        Assert.Equal(1, StrengthAt(23));
        Assert.Equal(1, StrengthAt(3));   // past midnight
    }

    [Fact]
    public void It_fades_in_at_the_start_and_out_at_the_end()
    {
        Assert.Equal(0, StrengthAt(20, 0), 3);
        Assert.Equal(0.5, StrengthAt(20, 15), 3);
        Assert.Equal(1, StrengthAt(20, 30), 3);
        Assert.Equal(0.5, StrengthAt(7, 45), 3);
        Assert.Equal(0, StrengthAt(8, 0), 3);
    }

    [Fact]
    public void Transition_0_turns_the_tint_on_at_once()
    {
        var start = new TimeOnly(20, 0);
        Assert.Equal(1, Schedule.StrengthAt(start, start, End, 0), 3);
        Assert.Equal(1, Schedule.StrengthAt(start.Add(TimeSpan.FromSeconds(1)), start, End, 0), 3);
    }

    [Fact]
    public void A_10_second_transition_fades_in_over_10_seconds()
    {
        var start = new TimeOnly(20, 0);
        Assert.Equal(0.5, Schedule.StrengthAt(start.Add(TimeSpan.FromSeconds(5)), start, End, 10), 3);
        Assert.Equal(1, Schedule.StrengthAt(start.Add(TimeSpan.FromSeconds(10)), start, End, 10), 3);
    }

    [Fact]
    public void A_fade_never_takes_more_than_half_the_range()
    {
        // 30 minute range with a 30 minute transition: full strength is reached halfway.
        var start = new TimeOnly(20, 0);
        var end = new TimeOnly(20, 30);
        Assert.Equal(1, Schedule.StrengthAt(new TimeOnly(20, 15), start, end, 30 * 60), 3);
    }

    [Fact]
    public void A_whole_day_range_is_on_all_day()
    {
        var noon = new TimeOnly(12, 0);
        Assert.Equal(1, Schedule.StrengthAt(noon, new TimeOnly(8, 0), new TimeOnly(8, 0), 60), 3);
    }

    [Fact]
    public void A_whole_day_range_has_no_edges_to_fade_at()
    {
        // Start == end: fully on even right at the start time, and never fading, whatever the transition.
        var eight = new TimeOnly(8, 0);
        int[] transitions = [0, 10, 30 * 60];
        int[] moments = [-60, -5, -1, 0, 1, 5, 60];
        foreach (var transition in transitions)
            foreach (var seconds in moments)
            {
                var now = eight.Add(TimeSpan.FromSeconds(seconds));
                Assert.Equal(1, Schedule.StrengthAt(now, eight, eight, transition));
                Assert.False(Schedule.IsFading(now, eight, eight, transition, IdleCheck));
            }
    }

    [Fact]
    public void Fading_is_reported_only_around_the_edges()
    {
        Assert.True(IsFading(new TimeOnly(20, 10)));
        Assert.True(IsFading(new TimeOnly(7, 50)));
        Assert.True(IsFading(new TimeOnly(19, 59, 56)));   // about to start: the next idle check would come too late
        Assert.False(IsFading(new TimeOnly(19, 45)));       // too early to start checking often
        Assert.True(IsFading(new TimeOnly(7, 29, 56)));     // about to fade out, which begins at 07:30
        Assert.False(IsFading(new TimeOnly(23, 0)));
        Assert.False(IsFading(new TimeOnly(12, 0)));
    }

    [Fact]
    public void Checking_every_5_seconds_never_notices_a_fade_in_or_out_late()
    {
        // A 10 s transition: the fade-in begins at 20:00 and the fade-out at 07:59:50. However the idle checks fall
        // against the clock, the app starts checking often before either one begins.
        TimeOnly[] fadesBegin = [new(20, 0), new(7, 59, 50)];
        foreach (var fadeBegins in fadesBegin)
            for (var tenths = 0; tenths < 50; tenths++)
            {
                var check = fadeBegins.Add(TimeSpan.FromSeconds(tenths / 10.0 - 120));
                while (!IsFading(check, transition: 10)) check = check.Add(IdleCheck);
                Assert.True(check <= fadeBegins, $"first noticed at {check:HH:mm:ss.f}");
            }
    }
}
