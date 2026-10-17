namespace Amber;

/// <summary>
/// Reads the advanced timeline at a moment in time. Filters never overlap, but their fades do: every edge is a
/// 10 s ramp centred on it, so a filter starts appearing 5 s before its start and is fully on 5 s after, and
/// mirrors that at its end. Two filters that touch ramp over the same 10 s with weights adding up to 1 — a
/// cross-fade that never shows a clear screen — while a gap fades out to clear and back in.
/// </summary>
public static class AdvancedSchedule
{
    // Always 10 s, and not Fade.DefaultSeconds: that one keeps on/off instant (the owner's choice), but a hand-over
    // between filters should never snap, and advanced mode has no transition setting.
    public const double FadeSeconds = 10;
    const double DaySeconds = 24 * 60 * 60;

    /// <summary>Every filter that is showing at <paramref name="now"/>, with how strongly (0–1).</summary>
    public static List<(Filter Filter, double Weight)> Weights(IEnumerable<Filter> filters, TimeOnly now)
    {
        var showing = new List<(Filter, double)>();
        foreach (var filter in filters)
        {
            var weight = WeightOf(filter, now);
            if (weight > 0) showing.Add((filter, weight));
        }
        return showing;
    }

    /// <summary>
    /// The level the filter asks for at <paramref name="now"/> — its flat level, or its curve. While it fades in
    /// before its start or out after its end, the level at that edge.
    /// </summary>
    public static double LevelAt(Filter filter, TimeOnly now)
    {
        var elapsed = SecondsSince(filter.Start, now);
        // Past the middle of the gap the next start is nearer: that's just before the start, not long after the end.
        if (elapsed > (filter.LengthMinutes * 60.0 + DaySeconds) / 2) elapsed -= DaySeconds;
        return filter.LevelAt((int)Math.Clamp(elapsed / 60, 0, filter.LengthMinutes));
    }

    /// <summary>
    /// True while a fade is running or begins within <paramref name="lookAhead"/>. The app passes the time to its
    /// next idle check, so it is already checking often when a ramp begins and never notices one late.
    /// </summary>
    public static bool IsFading(IEnumerable<Filter> filters, TimeOnly now, TimeSpan lookAhead) =>
        filters.Any(filter =>
        {
            var length = filter.LengthMinutes * 60.0;
            if (length >= DaySeconds) return false;   // a whole-day filter has no edges

            var elapsed = SecondsSince(filter.Start, now);
            bool Near(double fromEdge) => fromEdge >= -FadeSeconds / 2 - lookAhead.TotalSeconds && fromEdge < FadeSeconds / 2;
            return Near(Signed(elapsed)) || Near(Signed(elapsed - length));
        });

    static double WeightOf(Filter filter, TimeOnly now)
    {
        var length = filter.LengthMinutes * 60.0;
        if (length >= DaySeconds) return 1;   // a whole-day filter has no edges

        // Seconds from each edge, negative before it. Each ramp runs from half a fade before its edge to half a fade
        // after; a filter and a gap both last at least a minute, so one filter's two ramps never meet.
        var elapsed = SecondsSince(filter.Start, now);
        var (fromStart, fromEnd) = (Signed(elapsed), Signed(elapsed - length));
        if (Math.Abs(fromStart) < FadeSeconds / 2) return 0.5 + fromStart / FadeSeconds;
        if (Math.Abs(fromEnd) < FadeSeconds / 2) return 0.5 - fromEnd / FadeSeconds;
        return elapsed < length ? 1 : 0;
    }

    static double SecondsSince(TimeOnly start, TimeOnly now) =>
        ((now.ToTimeSpan() - start.ToTimeSpan()).TotalSeconds % DaySeconds + DaySeconds) % DaySeconds;

    // Seconds the short way round the clock: just before midnight is a little before 0, not almost a day after it.
    static double Signed(double seconds)
    {
        var wrapped = (seconds % DaySeconds + DaySeconds) % DaySeconds;
        return wrapped < DaySeconds / 2 ? wrapped : wrapped - DaySeconds;
    }
}
