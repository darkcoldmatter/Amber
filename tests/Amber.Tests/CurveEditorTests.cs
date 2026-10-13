using System.Windows;
using Amber.Controls;

namespace Amber.Tests;

public class CurveEditorTests
{
    const double PlotWidth = 750;

    // 7 px per character is close enough to the real font for the spacing rules.
    static List<TimeLabel> Labels(int startHour, int startMinute, int length) =>
        CurveEditor.TimeLabels(startHour * 60 + startMinute, length, PlotWidth, text => text.Length * 7);

    [Fact]
    public void Labels_come_every_half_hour_when_there_is_room()
    {
        var labels = Labels(6, 0, 240);

        Assert.Equal(["6:00", "6:30", "7:00", "7:30", "8:00", "8:30", "9:00", "9:30", "10:00"], labels.Select(l => l.Text));
        Assert.Equal([0, 30, 60, 90, 120, 150, 180, 210, 240], labels.Select(l => l.Minutes));
        Assert.Equal([false, true, false, true, false, true, false, true, false], labels.Select(l => l.Faded));
    }

    [Fact]
    public void Tight_labels_thin_out_to_whole_hours_and_then_fewer()
    {
        var halfDay = Labels(0, 0, 12 * 60);
        Assert.All(halfDay, label => Assert.Equal(0, label.Minutes % 60));
        Assert.True(halfDay.Count >= 12);

        var day = Labels(0, 0, 24 * 60);
        Assert.All(day, label => Assert.Equal(0, label.Minutes % 120));
        Assert.Equal(13, day.Count);
        Assert.All(day, label => Assert.False(label.Faded));
    }

    [Fact]
    public void The_first_and_last_labels_always_show()
    {
        // 6:00–13:00: every half hour fits, but 12:30 would crowd 13:00, which is pinned to the right edge (and 6:30 would crowd 6:00).
        var labels = Labels(6, 0, 7 * 60);

        Assert.Equal("6:00", labels[0].Text);
        Assert.Equal("13:00", labels[^1].Text);
        Assert.Equal("12:00", labels[^2].Text);
    }

    [Fact]
    public void Labels_sit_on_the_clock_even_when_the_range_does_not()
    {
        Assert.Equal(["6:30", "7:00", "7:30", "8:00"], Labels(6, 15, 120).Select(l => l.Text));
    }

    [Fact]
    public void A_range_under_an_hour_shows_its_two_times()
    {
        var labels = Labels(23, 30, 45);

        Assert.Equal(["23:30", "0:15"], labels.Select(l => l.Text));
        Assert.Equal([0, 45], labels.Select(l => l.Minutes));
    }

    static readonly Rect Card = new(0, 0, 800, 241);
    static readonly Size Box = new(50, 40);

    [Fact]
    public void The_value_box_sits_to_the_left_of_the_point()
    {
        Assert.Equal(new Rect(336, 80, 50, 40), CurveEditor.ValueBoxAt(new Point(400, 100), Box, Card));
    }

    [Fact]
    public void Without_room_on_the_left_the_value_box_moves_to_the_right()
    {
        Assert.Equal(new Rect(44, 80, 50, 40), CurveEditor.ValueBoxAt(new Point(30, 100), Box, Card));
    }

    [Fact]
    public void The_value_box_never_leaves_the_card()
    {
        Assert.Equal(0, CurveEditor.ValueBoxAt(new Point(400, 5), Box, Card).Top);
        Assert.Equal(Card.Bottom - Box.Height, CurveEditor.ValueBoxAt(new Point(400, 240), Box, Card).Top);
    }
}
