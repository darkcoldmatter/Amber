using System.Globalization;

namespace Amber.Tests;

public class ClockFormatTests
{
    static readonly ClockFormat TwelveHour = new(CultureInfo.GetCultureInfo("en-US"));
    static readonly ClockFormat TwentyFourHour = new(CultureInfo.GetCultureInfo("ru-RU"));

    [Fact]
    public void Follows_the_culture_clock_format()
    {
        Assert.False(TwelveHour.Is24Hour);
        Assert.True(TwentyFourHour.Is24Hour);
    }

    [Fact]
    public void Twelve_hour_clock_lists_1_to_12_with_AM_and_PM()
    {
        Assert.Equal(12, TwelveHour.HourLabels.Count);
        Assert.Equal("1", TwelveHour.HourLabels[0]);
        Assert.Equal("12", TwelveHour.HourLabels[^1]);
        Assert.Equal(["AM", "PM"], TwelveHour.PeriodLabels);
    }

    [Fact]
    public void Twenty_four_hour_clock_lists_24_hours()
    {
        Assert.Equal(24, TwentyFourHour.HourLabels.Count);
        Assert.EndsWith("0", TwentyFourHour.HourLabels[0]); // "0" or "00", depending on the culture data version
        Assert.Equal("23", TwentyFourHour.HourLabels[^1]);
    }

    [Fact]
    public void Minutes_go_in_5_minute_steps()
    {
        Assert.Equal(12, TwelveHour.MinuteLabels.Count);
        Assert.Equal("00", TwelveHour.MinuteLabels[0]);
        Assert.Equal("05", TwelveHour.MinuteLabels[1]);
        Assert.Equal("55", TwelveHour.MinuteLabels[^1]);
    }

    [Theory]
    [InlineData(11, 0, 0, 0, 0)]   // 12:00 AM
    [InlineData(11, 6, 0, 0, 30)]  // 12:30 AM
    [InlineData(0, 0, 0, 1, 0)]    // 1:00 AM
    [InlineData(11, 0, 1, 12, 0)]  // 12:00 PM
    [InlineData(8, 3, 1, 21, 15)]  // 9:15 PM
    [InlineData(10, 11, 1, 23, 55)] // 11:55 PM
    public void Twelve_hour_selection_gives_the_right_time(int hour, int minute, int period, int expectedHour, int expectedMinute)
    {
        Assert.Equal(new TimeOnly(expectedHour, expectedMinute), TwelveHour.TimeAt(hour, minute, period));
    }

    [Fact]
    public void Every_5_minutes_round_trips_in_both_formats()
    {
        foreach (var clock in new[] { TwelveHour, TwentyFourHour })
        {
            for (var minutes = 0; minutes < 24 * 60; minutes += 5)
            {
                var time = TimeOnly.MinValue.AddMinutes(minutes);
                var (hour, minute, period) = clock.IndexesOf(time);
                Assert.Equal(time, clock.TimeAt(hour, minute, period));
            }
        }
    }

    [Fact]
    public void Time_between_steps_rounds_down()
    {
        Assert.Equal((8, 1, 1), TwelveHour.IndexesOf(new TimeOnly(21, 7)));
    }
}
