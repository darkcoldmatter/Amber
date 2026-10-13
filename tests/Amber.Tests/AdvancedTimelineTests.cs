using Amber.Controls;

namespace Amber.Tests;

public class AdvancedTimelineTests
{
    const int Width = 240; // 10 px per hour

    static (int, int) Range(double startHour, double endHour) => ((int)(startHour * 60), (int)(endHour * 60));

    [Fact]
    public void A_bar_is_grabbed_by_its_own_edges_and_picked_in_between()
    {
        (int, int)[] bars = [Range(6, 10)];

        Assert.Equal(new BarHit(0, true), AdvancedTimeline.HitTest(62, bars, Width));
        Assert.Equal(new BarHit(0, false), AdvancedTimeline.HitTest(98, bars, Width));
        Assert.Equal(new BarHit(0, null), AdvancedTimeline.HitTest(80, bars, Width));
    }

    [Fact]
    public void Where_two_bars_touch_the_edge_belongs_to_the_bar_under_the_pointer()
    {
        (int, int)[] bars = [Range(6, 10), Range(10, 14)];

        Assert.Equal(new BarHit(0, false), AdvancedTimeline.HitTest(99, bars, Width));
        Assert.Equal(new BarHit(1, true), AdvancedTimeline.HitTest(101, bars, Width));
    }

    [Fact]
    public void Next_to_a_bar_its_edge_is_still_within_reach()
    {
        (int, int)[] bars = [Range(6, 10)];

        Assert.Equal(new BarHit(0, true), AdvancedTimeline.HitTest(60 - Timeline.EdgeReach, bars, Width));
        Assert.Equal(BarHit.None, AdvancedTimeline.HitTest(60 - Timeline.EdgeReach - 1, bars, Width));
        Assert.Equal(BarHit.None, AdvancedTimeline.HitTest(160, bars, Width));
    }

    [Fact]
    public void A_bar_past_midnight_is_grabbed_at_both_ends_of_the_day()
    {
        (int, int)[] bars = [Range(22, 2)];

        Assert.Equal(new BarHit(0, true), AdvancedTimeline.HitTest(222, bars, Width));
        Assert.Equal(new BarHit(0, false), AdvancedTimeline.HitTest(18, bars, Width));
        Assert.Equal(new BarHit(0, null), AdvancedTimeline.HitTest(5, bars, Width));
    }
}
