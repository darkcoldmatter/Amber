namespace Amber.Tests;

public class TimeOptionsTests
{
    [Fact]
    public void Minutes_come_in_15_minute_steps()
    {
        Assert.Equal(["00", "15", "30", "45"], TimeOptions.Minutes);
    }

    [Fact]
    public void Transition_tops_out_at_30_minutes()
    {
        Assert.Equal(30 * 60, TimeOptions.Transitions(12 * 60)[^1]);
    }

    [Fact]
    public void Transition_never_takes_more_than_half_the_schedule()
    {
        Assert.Equal([0, 10, 30, 60, 120, 300], TimeOptions.Transitions(15));   // 15 min schedule → at most 7.5 min
        Assert.Equal([0, 10, 30, 60, 120, 300, 600, 900], TimeOptions.Transitions(30));
    }

    [Fact]
    public void A_12_hour_schedule_offers_every_transition()
    {
        Assert.Equal([0, 10, 30, 60, 120, 300, 600, 900, 1800], TimeOptions.Transitions(12 * 60));
    }

    [Fact]
    public void Instant_is_always_offered()
    {
        Assert.Equal(0, TimeOptions.Transitions(15)[0]);
    }

    [Fact]
    public void Transition_labels_say_Off_seconds_or_minutes()
    {
        Assert.Equal("Off", TimeOptions.TransitionLabel(0));
        Assert.Equal("10 s", TimeOptions.TransitionLabel(10));
        Assert.Equal("30 s", TimeOptions.TransitionLabel(30));
        Assert.Equal("1 min", TimeOptions.TransitionLabel(60));
        Assert.Equal("2 min", TimeOptions.TransitionLabel(120));
        Assert.Equal("30 min", TimeOptions.TransitionLabel(1800));
    }
}
