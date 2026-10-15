using System.Globalization;
using System.Runtime.InteropServices;

namespace Amber;

/// <summary>
/// How Amber writes a time: on the 12-hour clock ("6:30 PM") or the 24-hour one ("18:30"), whichever Windows' region
/// settings use. Only that choice comes from Windows; the words stay English like the rest of Amber, so it is always
/// "AM" and "PM", after the time. Every time Amber shows is written here; on the 24-hour clock each one reads as it
/// always has.
/// </summary>
public sealed class TimeFormat
{
    const string AM = "AM", PM = "PM";
    const uint LOCALE_SSHORTTIME = 0x79;

    static TimeFormat? _current;

    TimeFormat(bool is24Hour) => Is24Hour = is24Hour;

    /// <summary>The clock Windows used when the window last opened.</summary>
    public static TimeFormat Current => _current ??= Read();

    /// <summary>Reads Windows' clock again. The window does this each time it opens, so a change shows the next time.</summary>
    public static void Refresh() => _current = Read();

    /// <summary>
    /// The clock a short time format asks for: 24-hour when it has an "H" outside its literal text (anything in quotes,
    /// or a character after a backslash), else 12-hour.
    /// </summary>
    public static TimeFormat For(string shortTime)
    {
        char? quote = null;
        for (var i = 0; i < shortTime.Length; i++)
        {
            var c = shortTime[i];
            if (c == '\\') i++;
            else if (quote is not null) { if (c == quote) quote = null; }
            else if (c is '\'' or '"') quote = c;
            else if (c == 'H') return new TimeFormat(true);
        }
        return new TimeFormat(false);
    }

    public bool Is24Hour { get; }

    /// <summary>AM or PM for an hour of the day (0–24); nothing on the 24-hour clock.</summary>
    public string Designator(int hour) => Is24Hour ? "" : hour % 24 < 12 ? AM : PM;

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

    /// <summary>
    /// Windows' short time format as the user has it set right now (.NET keeps its own copy from when Amber started), or
    /// .NET's copy if Windows can't say.
    /// </summary>
    static TimeFormat Read()
    {
        var buffer = new char[80]; // the longest a time format can be, closing null included
        var length = GetLocaleInfoEx(null, LOCALE_SSHORTTIME, buffer, buffer.Length);
        return For(length > 0 ? new string(buffer, 0, length - 1) : CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern);
    }

    static (int Hour, int Minute) HourAndMinute(int minutes)
    {
        var inDay = (minutes % ScheduleRange.DayMinutes + ScheduleRange.DayMinutes) % ScheduleRange.DayMinutes;
        return (inDay / 60, inDay % 60);
    }

    static int Twelve(int hour) => hour % 12 == 0 ? 12 : hour % 12;

    static string Number(int value, string format = "0") => value.ToString(format, CultureInfo.InvariantCulture);

    // A null locale name is the user's own locale (LOCALE_NAME_USER_DEFAULT), with their changes to it.
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern int GetLocaleInfoEx(string? localeName, uint type, [Out] char[] data, int size);
}
