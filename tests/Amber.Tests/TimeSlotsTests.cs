using System.Globalization;

namespace Amber.Tests;

public class TimeSlotsTests
{
    static readonly TimeSlots TwelveHour = new(CultureInfo.GetCultureInfo("en-US"));
    static readonly TimeSlots TwentyFourHour = new(CultureInfo.GetCultureInfo("ru-RU"));

    [Fact]
    public void Follows_the_culture_clock_format()
    {
        Assert.False(TwelveHour.Is24Hour);
        Assert.True(TwentyFourHour.Is24Hour);
    }

    [Fact]
    public void Twelve_hour_clock_has_48_slots_starting_at_12()
    {
        Assert.Equal(48, TwelveHour.ClockLabels.Count);
        Assert.Equal("12:00", TwelveHour.ClockLabels[0]);
        Assert.Equal("1:00", TwelveHour.ClockLabels[4]);
        Assert.Equal("11:45", TwelveHour.ClockLabels[^1]);
        Assert.Equal(["AM", "PM"], TwelveHour.PeriodLabels);
    }

    [Fact]
    public void Twenty_four_hour_clock_has_96_slots()
    {
        Assert.Equal(96, TwentyFourHour.ClockLabels.Count);
        Assert.Contains(TwentyFourHour.ClockLabels[0], new[] { "0:00", "00:00" }); // depends on the culture data version
        Assert.Equal("23:45", TwentyFourHour.ClockLabels[^1]);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]    // 12:00 AM
    [InlineData(2, 0, 0, 30)]   // 12:30 AM
    [InlineData(0, 1, 12, 0)]   // 12:00 PM
    [InlineData(36, 1, 21, 0)]  // 9:00 PM
    [InlineData(47, 1, 23, 45)] // 11:45 PM
    public void Twelve_hour_selection_gives_the_right_time(int clock, int period, int hour, int minute)
    {
        Assert.Equal(new TimeOnly(hour, minute), TwelveHour.TimeAt(clock, period));
    }

    [Fact]
    public void Every_slot_round_trips_in_both_formats()
    {
        foreach (var slots in new[] { TwelveHour, TwentyFourHour })
        {
            for (var minutes = 0; minutes < 24 * 60; minutes += 15)
            {
                var time = TimeOnly.MinValue.AddMinutes(minutes);
                var (clock, period) = slots.IndexesOf(time);
                Assert.Equal(time, slots.TimeAt(clock, period));
            }
        }
    }

    [Fact]
    public void Time_between_slots_rounds_down()
    {
        Assert.Equal((36, 1), TwelveHour.IndexesOf(new TimeOnly(21, 7)));
    }
}
