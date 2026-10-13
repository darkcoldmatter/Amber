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
    public void Tight_labels_thin_out_to_a_wider_stride_on_the_clock()
    {
        Assert.Equal(["0:00", "2:00", "4:00", "6:00", "8:00", "10:00", "12:00"], Labels(0, 0, 12 * 60).Select(l => l.Text));

        var day = Labels(0, 0, 24 * 60);
        Assert.Equal(13, day.Count);
        Assert.All(day, label => Assert.Equal(0, label.Minutes % 120));
        Assert.All(day, label => Assert.False(label.Faded));
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(0, 24)]
    [InlineData(6, 7)]
    [InlineData(3, 20)]
    [InlineData(6, 14)]
    [InlineData(21, 5)]
    public void Thinning_keeps_a_regular_stride_from_end_to_end(int startHour, int hours)
    {
        var minutes = Labels(startHour, 0, hours * 60).Select(l => l.Minutes).ToList();

        Assert.Equal(0, minutes[0]);
        Assert.Equal(hours * 60, minutes[^1]);
        Assert.Single(minutes.Zip(minutes.Skip(1), (a, b) => b - a).Distinct());
    }

    [Fact]
    public void Ends_off_the_stride_still_show_and_a_label_running_into_one_gives_way()
    {
        // 6:15–8:15: half hours from 6:30, and the exact ends.
        Assert.Equal(["6:15", "6:30", "7:00", "7:30", "8:00", "8:15"], Labels(6, 15, 120).Select(l => l.Text));

        // 6:15–18:15: whole hours from 7:00; 18:00 would run into 18:15, so it gives way, and only it.
        Assert.Equal(["6:15", .. Enumerable.Range(7, 11).Select(hour => $"{hour}:00"), "18:15"], Labels(6, 15, 12 * 60).Select(l => l.Text));
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
