using System.Globalization;

namespace Amber;

/// <summary>
/// How Amber writes a time: on the 12-hour clock ("6:30 PM") or the 24-hour one ("18:30"), whichever Windows' region
/// settings use (24-hour when the short time format has an "H"). Every time Amber shows is written here; on the
/// 24-hour clock each one reads as it always has.
/// </summary>
public sealed class TimeFormat
{
    readonly string _am, _pm;

    TimeFormat(bool is24Hour, string am, string pm) => (Is24Hour, _am, _pm) = (is24Hour, am, pm);

    /// <summary>The clock Windows uses. The window reads Windows' settings afresh each time it opens.</summary>
    public static TimeFormat Current => For(CultureInfo.CurrentCulture);

    /// <summary>A culture's clock: 24-hour when its short time format uses "H", else 12-hour with its own AM and PM.</summary>
    public static TimeFormat For(CultureInfo culture)
    {
        var format = culture.DateTimeFormat;
        return new TimeFormat(format.ShortTimePattern.Contains('H'), format.AMDesignator, format.PMDesignator);
    }

    public bool Is24Hour { get; }

    /// <summary>AM or PM for an hour of the day (0–24); nothing on the 24-hour clock.</summary>
    public string Designator(int hour) => Is24Hour ? "" : hour % 24 < 12 ? _am : _pm;

    /// <summary>The hour in a time field: "06", or "6" (with AM or PM beside it).</summary>
    public string FieldHour(int hour) => Is24Hour ? Number(hour, "00") : Number(Twelve(hour));

    /// <summary>The hours a time field offers, from midnight on: "00" to "23", or "12 AM", "1 AM" … "11 PM".</summary>
    public IReadOnlyList<string> Hours =>
        [.. Enumerable.Range(0, 24).Select(hour => Is24Hour ? FieldHour(hour) : $"{FieldHour(hour)} {Designator(hour)}")];

    /// <summary>A timeline's label for a whole hour (0–24): "00" to "24", or "12 AM", "2" … "12 PM" … "12 AM", with AM or PM at 12 only.</summary>
    public string TimelineHour(int hour) =>
        Is24Hour ? Number(hour, "00") : hour % 12 == 0 ? $"12 {Designator(hour)}" : Number(hour % 12);

    /// <summary>A time in full, from minutes after midnight (past the next midnight too): "6:00" and "23:30", or "6:00 AM" and "11:30 PM".</summary>
    public string Clock(int minutes)
    {
        var (hour, minute) = HourAndMinute(minutes);
        return Is24Hour ? $"{Number(hour)}:{Number(minute, "00")}" : $"{Number(Twelve(hour))}:{Number(minute, "00")} {Designator(hour)}";
    }

    /// <summary>
    /// A time over the curve. On the 24-hour clock as <see cref="Clock"/>: "6:00", "6:30". On the 12-hour one "6" on the
    /// hour and "6:30" otherwise, with AM or PM at 12 o'clock and wherever <paramref name="designator"/> asks for it.
    /// </summary>
    public string GraphLabel(int minutes, bool designator)
    {
        if (Is24Hour) return Clock(minutes);

        var (hour, minute) = HourAndMinute(minutes);
        var time = minute == 0 ? Number(Twelve(hour)) : $"{Number(Twelve(hour))}:{Number(minute, "00")}";
        return designator || (minute == 0 && hour % 12 == 0) ? $"{time} {Designator(hour)}" : time;
    }

    static (int Hour, int Minute) HourAndMinute(int minutes)
    {
        var inDay = (minutes % ScheduleRange.DayMinutes + ScheduleRange.DayMinutes) % ScheduleRange.DayMinutes;
        return (inDay / 60, inDay % 60);
    }

    static int Twelve(int hour) => hour % 12 == 0 ? 12 : hour % 12;

    static string Number(int value, string format = "0") => value.ToString(format, CultureInfo.InvariantCulture);
}
