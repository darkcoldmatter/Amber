namespace Amber.Tests;

public class CurveMathTests
{
    const int Length = 480;   // an 8 hour range
    static List<CurvePoint> Flat(double level = 50) => CurveMath.Flat(Length, level);

    [Fact]
    public void Times_land_on_5_minute_marks_inside_the_range()
    {
        Assert.Equal(5, CurveMath.StepMinutes);
        Assert.Equal(55, CurveMath.SnapTime(57, Length));
        Assert.Equal(60, CurveMath.SnapTime(58, Length));
        Assert.Equal(0, CurveMath.SnapTime(-20, Length));
        Assert.Equal(Length, CurveMath.SnapTime(999, Length));
    }

    [Fact]
    public void A_level_close_to_another_points_lines_up_with_it()
    {
        Assert.Equal(75, CurveMath.SnapLevel(73, [75]));
        Assert.Equal(70, CurveMath.SnapLevel(70, [75]));     // 5 apart: left alone
        Assert.Equal(100, CurveMath.SnapLevel(140, []));     // clamped
    }

    [Fact]
    public void Of_two_close_levels_a_point_lines_up_with_the_nearer()
    {
        Assert.Equal(54, CurveMath.SnapLevel(52.5, [50, 54]));
        Assert.Equal(50, CurveMath.SnapLevel(51.5, [54, 50]));
    }

    [Fact]
    public void A_new_curve_is_flat_at_the_current_level()
    {
        Assert.Equal([new CurvePoint(0, 40), new CurvePoint(Length, 40)], CurveMath.Flat(Length, 40));
    }

    [Fact]
    public void Clicking_adds_a_point_in_time_order()
    {
        var curve = CurveMath.Add(Flat(), 132, 80, Length);

        Assert.Equal(3, curve.Count);
        Assert.Equal(new CurvePoint(130, 80), curve[1]);
    }

    [Fact]
    public void A_new_point_lines_up_with_a_level_already_on_the_curve()
    {
        var curve = CurveMath.Add(Flat(50), 240, 52, Length);

        Assert.Equal(new CurvePoint(240, 50), curve[1]);
    }

    [Fact]
    public void Points_cannot_be_added_on_top_of_the_ends_or_of_each_other()
    {
        Assert.Equal(2, CurveMath.Add(Flat(), 0, 80, Length).Count);
        Assert.Equal(2, CurveMath.Add(Flat(), Length, 80, Length).Count);

        var curve = CurveMath.Add(Flat(), 120, 80, Length);
        Assert.Equal(3, CurveMath.Add(curve, 120, 20, Length).Count);
    }

    [Fact]
    public void Right_click_removes_a_point_but_never_an_end()
    {
        var curve = CurveMath.Add(Flat(), 120, 80, Length);

        Assert.Equal(2, CurveMath.Remove(curve, 1).Count);
        Assert.Equal(3, CurveMath.Remove(curve, 0).Count);
        Assert.Equal(3, CurveMath.Remove(curve, 2).Count);
    }

    [Fact]
    public void The_first_and_last_points_only_move_up_and_down()
    {
        var moved = CurveMath.Move(Flat(), 0, 200, 90, Length);

        Assert.Equal(new CurvePoint(0, 90), moved[0]);
        Assert.Equal(new CurvePoint(Length, 50), moved[1]);
    }

    [Fact]
    public void A_middle_point_stays_between_its_neighbours()
    {
        var curve = CurveMath.Add(Flat(), 240, 80, Length);

        var pushedLeft = CurveMath.Move(curve, 1, -100, 80, Length);
        Assert.Equal(CurveMath.StepMinutes, pushedLeft[1].Minutes);

        var pushedRight = CurveMath.Move(curve, 1, 900, 80, Length);
        Assert.Equal(Length - CurveMath.StepMinutes, pushedRight[1].Minutes);
    }

    [Fact]
    public void A_dragged_point_snaps_level_with_its_neighbour()
    {
        var curve = CurveMath.Add(Flat(50), 240, 80, Length);

        var moved = CurveMath.Move(curve, 1, 240, 52, Length);
        Assert.Equal(50, moved[1].Level);
    }

    [Fact]
    public void A_dragged_point_snaps_level_with_any_other_point_not_only_its_neighbours()
    {
        List<CurvePoint> curve = [new(0, 50), new(120, 20), new(240, 80), new(360, 30), new(Length, 50)];

        // 360's neighbours sit at 80 and 50; the point at 120, two along, is at 20.
        Assert.Equal(new CurvePoint(360, 20), CurveMath.Move(curve, 3, 360, 22, Length)[3]);
    }

    [Fact]
    public void A_dragged_point_between_two_close_levels_takes_the_nearer()
    {
        List<CurvePoint> curve = [new(0, 50), new(120, 10), new(240, 54), new(Length, 10)];

        Assert.Equal(54, CurveMath.Move(curve, 1, 120, 52.5, Length)[1].Level);
        Assert.Equal(50, CurveMath.Move(curve, 1, 120, 51.5, Length)[1].Level);
    }

    [Fact]
    public void A_shorter_range_drops_the_points_that_fall_outside()
    {
        var curve = CurveMath.Add(Flat(), 300, 80, Length);

        var fitted = CurveMath.Fit(curve, 240, 50);

        Assert.Equal(2, fitted.Count);
        Assert.Equal(0, fitted[0].Minutes);
        Assert.Equal(240, fitted[^1].Minutes);
    }

    [Fact]
    public void A_longer_range_moves_the_end_instead_of_leaving_a_point()
    {
        var curve = CurveMath.Move(CurveMath.Add(Flat(), 120, 80, Length), 2, Length, 30, Length);

        // Dragging the end edge outward fits the curve again at every 15-minute step.
        for (var length = Length + 15; length <= Length + 60; length += 15) curve = CurveMath.Fit(curve, length, 50);

        Assert.Equal([new CurvePoint(0, 50), new CurvePoint(120, 80), new CurvePoint(Length + 60, 30)], curve);
    }

    [Fact]
    public void A_curve_reads_straight_between_its_points_and_flat_beyond_them()
    {
        List<CurvePoint> curve = [new(60, 20), new(120, 80)];

        Assert.Equal(20, CurveMath.LevelAt(curve, 0));
        Assert.Equal(50, CurveMath.LevelAt(curve, 90));
        Assert.Equal(80, CurveMath.LevelAt(curve, 200));
    }

    [Fact]
    public void A_curve_is_read_between_whole_minutes_too()
    {
        List<CurvePoint> curve = [new(0, 50), new(5, 100)];

        Assert.Equal(55, CurveMath.LevelAt(curve, 0.5), 9);
        Assert.Equal(75, CurveMath.LevelAt(curve, 2.5), 9);
        Assert.Equal(99, CurveMath.LevelAt(curve, 4.9), 9);
    }

    [Fact]
    public void The_steepest_stretch_counts_only_what_falls_between_the_two_moments()
    {
        List<CurvePoint> curve = [new(0, 0), new(10, 0), new(15, 100), new(60, 80)];   // flat, 20 a minute up, gently down

        Assert.Equal(0, CurveMath.Steepest(curve, 2, 8));
        Assert.Equal(20, CurveMath.Steepest(curve, 9, 11), 9);          // reaches into the steep stretch
        Assert.Equal(20 / 45.0, CurveMath.Steepest(curve, 20, 30), 9);  // going down counts as moving too
        Assert.Equal(0, CurveMath.Steepest(curve, 61, 70));             // past the last point it stays flat
    }

    [Fact]
    public void A_saved_curve_that_stops_short_is_carried_on_to_both_ends_in_the_same_shape()
    {
        // As older versions could save it: the range grew to 135 minutes while the curve still starts at 15 and stops at 60.
        List<CurvePoint> saved = [new(15, 1), new(30, 100), new(45, 100), new(60, 100)];

        var reached = CurveMath.ReachEnds(saved, 135);

        Assert.Equal([new CurvePoint(0, 1), .. saved, new CurvePoint(135, 100)], reached);
        for (var minutes = 0; minutes <= 135; minutes++) Assert.Equal(CurveMath.LevelAt(saved, minutes), CurveMath.LevelAt(reached, minutes));
    }

    [Fact]
    public void A_saved_curve_running_past_its_range_is_cut_at_the_end_at_the_level_it_has_there()
    {
        var reached = CurveMath.ReachEnds([new(0, 0), new(100, 100), new(200, 0)], 150);

        Assert.Equal([new CurvePoint(0, 0), new CurvePoint(100, 100), new CurvePoint(150, 50)], reached);
    }

    [Fact]
    public void A_curve_that_spans_its_range_keeps_every_point()
    {
        // Including a long trail of middle points, one every 15 minutes.
        List<CurvePoint> trail = [new(0, 50), .. Enumerable.Range(1, Length / 15 - 1).Select(i => new CurvePoint(i * 15, 50)), new(Length, 50)];

        Assert.Equal(trail, CurveMath.ReachEnds(trail, Length));
        Assert.Empty(CurveMath.ReachEnds([], Length));
    }
}
