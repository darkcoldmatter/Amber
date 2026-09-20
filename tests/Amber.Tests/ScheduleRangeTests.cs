
namespace Amber.Tests;

public class ScheduleRangeTests
{
    const int Day = ScheduleRange.DayMinutes;
    const int Width = 630;

    static int MinutesAt(double x) => ScheduleRange.MinutesAt(x, Width);

    [Fact]
    public void Pointer_position_snaps_to_15_minutes()
    {
        Assert.Equal(0, MinutesAt(0));
        Assert.Equal(12 * 60, MinutesAt(Width / 2.0));
        Assert.Equal(20 * 60 + 15, MinutesAt(Width * (20.25 / 24) + 2));
    }

    [Fact]
    public void Dragging_past_either_end_wraps_around_midnight()
    {
        Assert.Equal(60, MinutesAt(Width + Width / 24.0));   // one hour past 24:00
        Assert.Equal(23 * 60, MinutesAt(-Width / 24.0));      // one hour before 00:00
    }

    [Fact]
    public void Length_counts_over_midnight_and_start_equal_end_is_a_full_day()
    {
        Assert.Equal(10 * 60, ScheduleRange.Length(20 * 60, 6 * 60));
        Assert.Equal(Day, ScheduleRange.Length(8 * 60, 8 * 60));
    }

    [Fact]
    public void Moving_an_edge_keeps_the_other_one_still()
    {
        Assert.Equal((21 * 60, 8 * 60), ScheduleRange.MoveEdge(20 * 60, 8 * 60, movingStart: true, 21 * 60));
        Assert.Equal((20 * 60, 7 * 60), ScheduleRange.MoveEdge(20 * 60, 8 * 60, movingStart: false, 7 * 60));
    }

    [Fact]
    public void A_range_never_gets_shorter_than_15_minutes()
    {
        var (start, end) = ScheduleRange.MoveEdge(20 * 60, 21 * 60, movingStart: true, 20 * 60 + 50);
        Assert.Equal(15, ScheduleRange.Length(start, end));
    }

    [Fact]
    public void An_edge_cannot_jump_over_the_other_one()
    {
        // 20:00вЂ“21:00: dragging the start onto the end would mean a 24 h range, so the range stays as it was.
        Assert.Equal((20 * 60, 21 * 60), ScheduleRange.MoveEdge(20 * 60, 21 * 60, movingStart: true, 21 * 60));
    }

    [Fact]
    public void Dragging_the_long_way_round_fills_the_whole_day()
    {
        // 23:00вЂ“21:00 is already 22 h; pulling the start back to 21:00 closes the last gap.
        var (start, end) = ScheduleRange.MoveEdge(23 * 60, 21 * 60, movingStart: true, 21 * 60);
        Assert.Equal(Day, ScheduleRange.Length(start, end));
    }
}

