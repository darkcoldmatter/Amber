namespace Amber.Tests;

public class TimeOptionsTests
{
    [Fact]
    public void Hours_and_minutes_cover_the_day_in_15_minute_steps()
    {
        Assert.Equal(24, TimeOptions.Hours.Count);
        Assert.Equal("00", TimeOptions.Hours[0]);
        Assert.Equal("23", TimeOptions.Hours[^1]);
        Assert.Equal(["00", "15", "30", "45"], TimeOptions.Minutes);
    }

    [Fact]
    public void Transition_tops_out_at_30_minutes()
    {
        Assert.Equal(30, TimeOptions.Transitions(12 * 60)[^1]);
    }

    [Fact]
    public void Transition_never_takes_more_than_half_the_schedule()
    {
        Assert.Equal([0, 1, 2, 3, 5], TimeOptions.Transitions(15));   // 15 min schedule → at most 7 min
        Assert.Equal([0, 1, 2, 3, 5, 10, 15], TimeOptions.Transitions(30));
    }

    [Fact]
    public void Instant_is_always_offered()
    {
        Assert.Equal(0, TimeOptions.Transitions(15)[0]);
    }
}
