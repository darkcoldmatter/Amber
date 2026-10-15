namespace Amber.Tests;

public class TimeFormatTests
{
    static readonly TimeFormat TwentyFour = TimeFormat.For("HH:mm");
    static readonly TimeFormat Twelve = TimeFormat.For("h:mm tt");

    [Fact]
    public void The_clock_is_24_hour_when_the_short_time_format_uses_H()
    {
        Assert.True(TimeFormat.For("HH:mm").Is24Hour);
        Assert.True(TimeFormat.For("H:mm").Is24Hour);
        Assert.False(TimeFormat.For("h:mm tt").Is24Hour);
        Assert.False(TimeFormat.For("tt hh:mm").Is24Hour);
    }

    [Fact]
    public void Literal_text_in_the_format_never_counts_as_the_24_hour_H()
    {
        Assert.False(TimeFormat.For("h:mm 'Hr'").Is24Hour);
        Assert.False(TimeFormat.For("h:mm \"H\"").Is24Hour);
        Assert.False(TimeFormat.For(@"h:mm \H").Is24Hour);
        Assert.False(TimeFormat.For("h 'o''clock H' mm").Is24Hour); // '' inside a literal is a quote mark
        Assert.True(TimeFormat.For("HH 'h' mm").Is24Hour);
        Assert.True(TimeFormat.For("'h'H:mm").Is24Hour);
    }

    [Fact]
    public void On_the_24_hour_clock_every_time_reads_as_it_always_has()
    {
        Assert.Equal("06", TwentyFour.FieldHour(6));
        Assert.Equal("", TwentyFour.Designator(18));
        Assert.Equal(Enumerable.Range(0, 24).Select(hour => hour.ToString("00")), TwentyFour.Hours);
        Assert.Equal(Enumerable.Range(0, 13).Select(i => (2 * i).ToString("00")), Enumerable.Range(0, 13).Select(i => TwentyFour.TimelineHour(2 * i)));
        Assert.Equal("6:00", TwentyFour.Clock(6 * 60));
        Assert.Equal("23:30", TwentyFour.Clock(23 * 60 + 30));
        Assert.Equal("0:15", TwentyFour.Clock(24 * 60 + 15)); // past midnight
        Assert.Equal("6:30", TwentyFour.GraphLabel(6 * 60 + 30, designator: true));
    }

    [Fact]
    public void A_12_hour_time_field_shows_the_hour_without_a_leading_zero_and_AM_or_PM()
    {
        Assert.Equal(["12 AM", "6 AM", "12 PM", "6 PM", "11 PM"],
            new[] { 0, 6, 12, 18, 23 }.Select(hour => $"{Twelve.FieldHour(hour)} {Twelve.Designator(hour)}"));
    }

    [Fact]
    public void The_12_hour_list_runs_from_12_AM_to_11_PM()
    {
        var hours = Twelve.Hours;

        Assert.Equal(24, hours.Count);
        Assert.Equal(["12 AM", "1 AM", "2 AM"], hours.Take(3));
        Assert.Equal(["11 AM", "12 PM", "1 PM"], hours.Skip(11).Take(3));
        Assert.Equal("11 PM", hours[^1]);
    }

    [Fact]
    public void Twelve_hour_timeline_labels_say_AM_or_PM_only_at_12()
    {
        Assert.Equal(["12 AM", "2", "4", "6", "8", "10", "12 PM", "2", "4", "6", "8", "10", "12 AM"],
            Enumerable.Range(0, 13).Select(i => Twelve.TimelineHour(2 * i)));
    }

    [Fact]
    public void A_full_12_hour_time_has_its_minutes_and_AM_or_PM()
    {
        Assert.Equal("10:00 AM", Twelve.Clock(10 * 60));
        Assert.Equal("12:15 AM", Twelve.Clock(15));
        Assert.Equal("12:30 PM", Twelve.Clock(12 * 60 + 30));
        Assert.Equal("11:45 PM", Twelve.Clock(23 * 60 + 45));
        Assert.Equal("12:15 AM", Twelve.Clock(24 * 60 + 15)); // past midnight
    }

    [Fact]
    public void Twelve_hour_graph_labels_are_plain_on_the_hour_with_AM_or_PM_at_12_and_where_asked()
    {
        Assert.Equal("7", Twelve.GraphLabel(7 * 60, designator: false));
        Assert.Equal("7:30", Twelve.GraphLabel(7 * 60 + 30, designator: false));
        Assert.Equal("12 PM", Twelve.GraphLabel(12 * 60, designator: false));
        Assert.Equal("12 AM", Twelve.GraphLabel(24 * 60, designator: false));
        Assert.Equal("12:30", Twelve.GraphLabel(12 * 60 + 30, designator: false));
        Assert.Equal("11 AM", Twelve.GraphLabel(11 * 60, designator: true));
        Assert.Equal("6:15 PM", Twelve.GraphLabel(18 * 60 + 15, designator: true));
    }

    [Fact]
    public void AM_and_PM_are_always_English_and_after_the_time()
    {
        // Windows only decides 12- or 24-hour: a format with its AM/PM first still reads "6:00 PM".
        var format = TimeFormat.For("tt h:mm");

        Assert.Equal("6:00 PM", format.Clock(18 * 60));
        Assert.Equal("12 AM", format.TimelineHour(0));
    }
}
