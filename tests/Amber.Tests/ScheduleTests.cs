namespace Amber.Tests;

public class ScheduleTests
{
    static readonly TimeOnly Start = new(20, 0), End = new(8, 0);   // overnight, 12 hours

    static double StrengthAt(int hour, int minute = 0, int transition = 30) =>
        Schedule.StrengthAt(new TimeOnly(hour, minute), Start, End, transition);

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
    public void A_fade_never_takes_more_than_half_the_range()
    {
        // 30 minute range with a 30 minute transition: full strength is reached halfway.
        var start = new TimeOnly(20, 0);
        var end = new TimeOnly(20, 30);
        Assert.Equal(1, Schedule.StrengthAt(new TimeOnly(20, 15), start, end, 30), 3);
    }

    [Fact]
    public void A_whole_day_range_is_on_all_day()
    {
        var noon = new TimeOnly(12, 0);
        Assert.Equal(1, Schedule.StrengthAt(noon, new TimeOnly(8, 0), new TimeOnly(8, 0), 1), 3);
    }

    [Fact]
    public void Fading_is_reported_only_around_the_edges()
    {
        Assert.True(Schedule.IsFading(new TimeOnly(20, 10), Start, End, 30));
        Assert.True(Schedule.IsFading(new TimeOnly(7, 50), Start, End, 30));
        Assert.True(Schedule.IsFading(new TimeOnly(19, 45), Start, End, 30));  // about to start
        Assert.False(Schedule.IsFading(new TimeOnly(23, 0), Start, End, 30));
        Assert.False(Schedule.IsFading(new TimeOnly(12, 0), Start, End, 30));
    }
}
