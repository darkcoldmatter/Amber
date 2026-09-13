using System.Globalization;

namespace Amber;

/// <summary>Schedule time choices in 15-minute steps, in the user's 12- or 24-hour clock format.</summary>
public sealed class TimeSlots
{
    const int StepMinutes = 15;
    const int SlotsPerHalfDay = 12 * 60 / StepMinutes;

    public TimeSlots(CultureInfo culture)
    {
        var format = culture.DateTimeFormat;
        Is24Hour = !format.ShortTimePattern.Contains('h');

        var clockFormat = Is24Hour ? (format.ShortTimePattern.Contains("HH") ? "HH:mm" : "H:mm") : "h:mm";
        ClockLabels = Enumerable.Range(0, Is24Hour ? 2 * SlotsPerHalfDay : SlotsPerHalfDay)
            .Select(i => TimeOnly.MinValue.AddMinutes(i * StepMinutes).ToString(clockFormat, culture))
            .ToArray();

        PeriodLabels = string.IsNullOrEmpty(format.AMDesignator)
            ? ["AM", "PM"]
            : [format.AMDesignator, format.PMDesignator];
    }

    /// <summary>True: one "21:00" box. False: a "9:00" box plus an AM/PM box.</summary>
    public bool Is24Hour { get; }

    public IReadOnlyList<string> ClockLabels { get; }

    /// <summary>AM and PM labels; only used with the 12-hour clock.</summary>
    public IReadOnlyList<string> PeriodLabels { get; }

    public TimeOnly TimeAt(int clockIndex, int periodIndex)
    {
        var slot = Is24Hour ? clockIndex : clockIndex + periodIndex * SlotsPerHalfDay;
        return TimeOnly.MinValue.AddMinutes(slot * StepMinutes);
    }

    /// <summary>Box selections for a time (rounded down to the step). Period is -1 with the 24-hour clock.</summary>
    public (int Clock, int Period) IndexesOf(TimeOnly time)
    {
        var slot = (time.Hour * 60 + time.Minute) / StepMinutes;
        return Is24Hour ? (slot, -1) : (slot % SlotsPerHalfDay, slot / SlotsPerHalfDay);
    }
}
