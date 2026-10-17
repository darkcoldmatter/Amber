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

    [Fact]
    public void Grabbing_an_edge_a_few_pixels_off_doesnt_move_it()
    {
        // The 10:00 end sits at x 100; it is taken hold of 4 px inside it, and the pointer then wobbles by a pixel.
        Assert.Equal(H(10), Timeline.DragTarget(H(10), isStart: false, pressX: 96, x: 96, Width));
        Assert.Equal(H(10), Timeline.DragTarget(H(10), isStart: false, pressX: 96, x: 97, Width));
        Assert.Equal(H(6), Timeline.DragTarget(H(6), isStart: true, pressX: 65, x: 64, Width));
    }

    [Fact]
    public void A_dragged_edge_follows_the_pointers_travel_snapped_to_15_minutes()
    {
        Assert.Equal(H(11), Timeline.DragTarget(H(10), isStart: false, pressX: 96, x: 106, Width));
        Assert.Equal(H(9.5), Timeline.DragTarget(H(10), isStart: false, pressX: 96, x: 91, Width));
        Assert.Equal(H(10.5), Timeline.DragTarget(H(10), isStart: false, pressX: 96, x: 100.6, Width)); // 4.6 px = 27.6 min
    }

    [Fact]
    public void Edges_at_midnight_drag_from_their_own_end_of_the_timeline()
    {
        Assert.Equal(H(23), Timeline.DragTarget(0, isStart: false, pressX: 238, x: 228, Width));
        Assert.Equal(H(1), Timeline.DragTarget(0, isStart: true, pressX: 2, x: 12, Width));
        Assert.Equal(H(0.5), Timeline.DragTarget(H(23.5), isStart: false, pressX: 233, x: 243, Width)); // wraps past midnight
    }

    [Fact]
    public void Corners_shrink_with_the_bar_from_12_px_at_an_hour_to_4_px_at_15_minutes()
    {
        const double day = 24 * 60; // 1 px per minute

        Assert.Equal(12, Timeline.CornerRadius(60, day));
        Assert.Equal(12, Timeline.CornerRadius(8 * 60, day));
        Assert.Equal(4, Timeline.CornerRadius(15, day));
        Assert.Equal(4 + 8 / 3.0, Timeline.CornerRadius(30, day), 6);
        Assert.Equal(4 + 16 / 3.0, Timeline.CornerRadius(45, day), 6);
    }

    [Fact]
    public void A_corner_is_never_more_than_half_the_bar()
    {
        // Simple mode's 676 px day: a 15-minute range is about 7 px wide.
        var width = 15 * 676 / (24 * 60.0);

        Assert.Equal(width / 2, Timeline.CornerRadius(width, 676), 6);
    }
}
