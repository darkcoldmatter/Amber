namespace Amber;

/// <summary>Where filters may sit on the 24-hour timeline: they never overlap and never get shorter than 15 minutes.</summary>
public static class FilterLayout
{
    public const int MinMinutes = ScheduleRange.SnapMinutes;
    public const int NewFilterMinutes = 60;
    const int Day = ScheduleRange.DayMinutes;

    /// <summary>
    /// Where a new filter goes: the left-most free stretch, an hour long or as much as fits.
    /// Null when nothing of at least 15 minutes is free.
    /// </summary>
    public static (int Start, int End)? NextSlot(IEnumerable<(int Start, int End)> taken)
    {
        var busy = Occupancy(taken);
        for (var minute = 0; minute < Day; minute++)
        {
            if (busy[minute]) continue;
            if (minute > 0 && !busy[minute - 1]) continue;   // only look at the start of a free stretch

            var free = 0;
            while (free < Day && !busy[(minute + free) % Day]) free++;
            if (free >= MinMinutes) return (minute, (minute + Math.Min(free, NewFilterMinutes)) % Day);
        }
        return null;
    }

    public static bool HasRoom(IEnumerable<(int Start, int End)> taken) => NextSlot(taken) is not null;

    /// <summary>
    /// Moves one edge of <paramref name="range"/> towards <paramref name="target"/>, stopping at the neighbouring
    /// filters and at the 15-minute minimum. Like the simple bar, an edge never jumps over the other one.
    /// </summary>
    public static (int Start, int End) MoveEdge((int Start, int End) range, IEnumerable<(int Start, int End)> others, bool movingStart, int target)
    {
        var busy = Occupancy(others);
        var current = ScheduleRange.Length(range.Start, range.End);
        var wanted = movingStart ? ScheduleRange.Length(target, range.End) : ScheduleRange.Length(range.Start, target);
        if (Math.Abs(wanted - current) > Day / 2) return range;

        var room = current + FreeRun(busy, movingStart ? range.Start : range.End, movingStart ? -1 : 1);
        var length = Math.Clamp(wanted, MinMinutes, Math.Min(room, Day));
        return movingStart
            ? (((range.End - length) % Day + Day) % Day, range.End)
            : (range.Start, (range.Start + length) % Day);
    }

    /// <summary>
    /// Moves one edge of <paramref name="filter"/> as dragging and the time fields do: towards <paramref name="target"/>,
    /// stopping at the other filters in <paramref name="all"/>.
    /// </summary>
    public static void MoveEdge(Filter filter, IEnumerable<Filter> all, bool movingStart, int target)
    {
        var (start, end) = MoveEdge(filter.Range, all.Where(other => other != filter).Select(other => other.Range), movingStart, target);
        filter.Start = ScheduleRange.Time(start);
        filter.End = ScheduleRange.Time(end);
    }

    /// <summary>Free minutes running away from <paramref name="edge"/> in the given direction.</summary>
    static int FreeRun(bool[] busy, int edge, int direction)
    {
        var free = 0;
        while (free < Day)
        {
            var minute = ((edge + (direction < 0 ? -free - 1 : free)) % Day + Day) % Day;
            if (busy[minute]) break;
            free++;
        }
        return free;
    }

    static bool[] Occupancy(IEnumerable<(int Start, int End)> ranges)
    {
        var busy = new bool[Day];
        foreach (var (start, end) in ranges)
        {
            var length = ScheduleRange.Length(start, end);
            for (var offset = 0; offset < length; offset++) busy[(start + offset) % Day] = true;
        }
        return busy;
    }
}
