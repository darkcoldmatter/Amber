namespace Amber.Tests;

public class FilterLayoutTests
{
    static (int, int) Range(int startHour, int endHour) => (startHour * 60, endHour * 60);

    [Fact]
    public void The_first_filter_starts_at_midnight_and_lasts_an_hour()
    {
        Assert.Equal((0, 60), FilterLayout.NextSlot([]));
    }

    [Fact]
    public void A_new_filter_takes_the_left_most_free_stretch()
    {
        Assert.Equal(Range(1, 2), FilterLayout.NextSlot([Range(0, 1)]));
        Assert.Equal(Range(2, 3), FilterLayout.NextSlot([Range(0, 1), Range(1, 2), Range(6, 8)]));
    }

    [Fact]
    public void A_new_filter_is_shorter_when_that_is_all_that_fits()
    {
        // 00:00–01:00 and 01:30 round to midnight leave a 30 minute gap.
        Assert.Equal((60, 90), FilterLayout.NextSlot([(0, 60), (90, 0)]));
    }

    [Fact]
    public void There_is_no_room_when_every_gap_is_under_15_minutes()
    {
        Assert.False(FilterLayout.HasRoom([(0, 1430)]));      // 10 minutes left
        Assert.True(FilterLayout.HasRoom([(0, 1425)]));       // 15 minutes left
        Assert.False(FilterLayout.HasRoom([(0, 0)]));         // the whole day
    }

    [Fact]
    public void An_edge_stops_at_the_next_filter()
    {
        // 08:00–10:00 with a neighbour at 06:00–07:00: the start can only reach 07:00.
        var moved = FilterLayout.MoveEdge(Range(8, 10), [Range(6, 7)], movingStart: true, 5 * 60);
        Assert.Equal(Range(7, 10), moved);
    }

    [Fact]
    public void An_edge_stops_at_the_15_minute_minimum()
    {
        var moved = FilterLayout.MoveEdge(Range(8, 10), [], movingStart: false, 8 * 60 + 10);
        Assert.Equal((8 * 60, 8 * 60 + 15), moved);
    }

    [Fact]
    public void An_edge_cannot_jump_over_the_other_one()
    {
        Assert.Equal(Range(8, 10), FilterLayout.MoveEdge(Range(8, 10), [], movingStart: false, 8 * 60));
    }

    [Fact]
    public void Ranges_may_cross_midnight()
    {
        var moved = FilterLayout.MoveEdge((23 * 60, 30), [], movingStart: false, 2 * 60);
        Assert.Equal((23 * 60, 2 * 60), moved);
    }
}
