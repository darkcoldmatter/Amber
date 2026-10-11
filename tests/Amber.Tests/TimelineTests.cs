using Amber.Controls;

namespace Amber.Tests;

public class TimelineTests
{
    const int Width = 240; // 10 px per hour

    static int H(double hours) => (int)(hours * 60);

    [Fact]
    public void A_range_within_the_day_is_one_piece()
    {
        Assert.Equal([(60.0, 100.0)], Timeline.Pieces(H(6), H(10), Width));
    }

    [Fact]
    public void A_range_past_midnight_splits_in_two()
    {
        Assert.Equal([(220.0, 240.0), (0.0, 60.0)], Timeline.Pieces(H(22), H(6), Width));
    }

    [Fact]
    public void A_range_ending_at_midnight_runs_to_the_right_end()
    {
        Assert.Equal([(200.0, 240.0)], Timeline.Pieces(H(20), 0, Width));
    }

    [Fact]
    public void A_full_day_from_midnight_is_one_piece_and_any_other_full_day_splits_where_it_starts()
    {
        Assert.Equal([(0.0, 240.0)], Timeline.Pieces(0, 0, Width));
        Assert.Equal([(80.0, 240.0), (0.0, 80.0)], Timeline.Pieces(H(8), H(8), Width));
    }

    [Fact]
    public void Contains_follows_the_range_past_midnight()
    {
        Assert.True(Timeline.Contains(230, H(22), H(6), Width));
        Assert.True(Timeline.Contains(30, H(22), H(6), Width));
        Assert.False(Timeline.Contains(120, H(22), H(6), Width));
    }

    [Fact]
    public void Edges_are_grabbed_within_reach_only()
    {
        Assert.Equal(true, Timeline.EdgeAt(60 + Timeline.EdgeReach, H(6), H(10), Width));
        Assert.Equal(false, Timeline.EdgeAt(100 - Timeline.EdgeReach, H(6), H(10), Width));
        Assert.Null(Timeline.EdgeAt(80, H(6), H(10), Width));
        Assert.Null(Timeline.EdgeAt(60 - Timeline.EdgeReach - 1, H(6), H(10), Width));
    }

    [Fact]
    public void An_end_at_midnight_is_grabbed_at_the_right_end()
    {
        Assert.Equal(false, Timeline.EdgeAt(238, H(20), 0, Width));
        Assert.Null(Timeline.EdgeAt(2, H(20), 0, Width));
    }

    [Fact]
    public void On_a_short_range_the_nearer_edge_wins()
    {
        // 06:00–06:15 is 2.5 px wide, so both edges are in reach.
        Assert.Equal(true, Timeline.EdgeAt(60.5, H(6), H(6.25), Width));
        Assert.Equal(false, Timeline.EdgeAt(62, H(6), H(6.25), Width));
    }

    [Fact]
    public void Where_a_full_day_starts_and_ends_the_side_of_the_pointer_decides()
    {
        Assert.Equal(true, Timeline.EdgeAt(82, H(8), H(8), Width));
        Assert.Equal(false, Timeline.EdgeAt(78, H(8), H(8), Width));
    }
}
