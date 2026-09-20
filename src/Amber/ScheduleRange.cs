namespace Amber;

/// <summary>A start/end range on the 24-hour clock, in minutes since midnight. Ranges may run past midnight.</summary>
public static class ScheduleRange
{
    public const int SnapMinutes = 15;
    public const int DayMinutes = 24 * 60;

    public static int Minutes(TimeOnly time) => time.Hour * 60 + time.Minute;
    public static TimeOnly Time(int minutes) => new(minutes / 60 % 24, minutes % 60);

    /// <summary>Minutes since midnight at a point along a bar of the given width, snapped and wrapped into one day.</summary>
    public static int MinutesAt(double x, double width)
    {
        var raw = (int)Math.Round(x / width * DayMinutes / SnapMinutes) * SnapMinutes;
        return ((raw % DayMinutes) + DayMinutes) % DayMinutes;
    }

    /// <summary>Length of a range in minutes; start == end means the whole day.</summary>
    public static int Length(int start, int end)
    {
        var length = ((end - start) % DayMinutes + DayMinutes) % DayMinutes;
        return length == 0 ? DayMinutes : length;
    }

    /// <summary>
    /// Moves one edge to <paramref name="target"/>, keeping the range between 15 minutes and a full day.
    /// An edge never jumps over the other one — that would turn a short range into an almost-full day in one step;
    /// dragging the long way round is how you fill the whole day.
    /// </summary>
    public static (int Start, int End) MoveEdge(int start, int end, bool movingStart, int target)
    {
        var current = Length(start, end);
        var wanted = movingStart ? Length(target, end) : Length(start, target);
        if (Math.Abs(wanted - current) > DayMinutes / 2) return (start, end);

        var length = Math.Clamp(wanted, SnapMinutes, DayMinutes);
        return movingStart
            ? (((end - length) % DayMinutes + DayMinutes) % DayMinutes, end)
            : (start, (start + length) % DayMinutes);
    }
}
