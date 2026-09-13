using System.Globalization;

namespace Amber;

/// <summary>Hour, minute and AM/PM choices for the schedule, following the user's 12- or 24-hour clock setting.</summary>
public sealed class ClockFormat
{
    const int MinuteStep = 5;

    public ClockFormat(CultureInfo culture)
    {
        var format = culture.DateTimeFormat;
        Is24Hour = !format.ShortTimePattern.Contains('h');

        var hourFormat = format.ShortTimePattern.Contains("HH") ? "00" : "0";
        HourLabels = Is24Hour
            ? Enumerable.Range(0, 24).Select(h => h.ToString(hourFormat, CultureInfo.InvariantCulture)).ToArray()
            : Enumerable.Range(1, 12).Select(h => h.ToString(CultureInfo.InvariantCulture)).ToArray();
        MinuteLabels = Enumerable.Range(0, 60 / MinuteStep)
            .Select(i => (i * MinuteStep).ToString("00", CultureInfo.InvariantCulture))
            .ToArray();
        PeriodLabels = string.IsNullOrEmpty(format.AMDesignator)
            ? ["AM", "PM"]
            : [format.AMDesignator, format.PMDesignator];
    }

    /// <summary>True: hour 0–23. False: hour 1–12 plus an AM/PM choice.</summary>
    public bool Is24Hour { get; }

    public IReadOnlyList<string> HourLabels { get; }
    public IReadOnlyList<string> MinuteLabels { get; }

    /// <summary>AM and PM labels; only used with the 12-hour clock.</summary>
    public IReadOnlyList<string> PeriodLabels { get; }

    public TimeOnly TimeAt(int hourIndex, int minuteIndex, int periodIndex)
    {
        // The 12-hour list is 1…12, so index 11 is "12", which is hour 0 (12 AM) or 12 (12 PM).
        var hour = Is24Hour ? hourIndex : (hourIndex + 1) % 12 + periodIndex * 12;
        return new TimeOnly(hour, minuteIndex * MinuteStep);
    }

    /// <summary>Selections for a time, minutes rounded down to the step. Period is -1 with the 24-hour clock.</summary>
    public (int Hour, int Minute, int Period) IndexesOf(TimeOnly time)
    {
        var minute = time.Minute / MinuteStep;
        if (Is24Hour) return (time.Hour, minute, -1);
        return ((time.Hour + 11) % 12, minute, time.Hour < 12 ? 0 : 1);
    }
}
