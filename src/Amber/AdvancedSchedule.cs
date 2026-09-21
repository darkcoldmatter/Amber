namespace Amber;

/// <summary>
/// Reads the advanced timeline at a moment in time. Filters never overlap, but their fades do:
/// a filter starts fading in 30 s before its time and keeps fading out for 30 s after it, so two
/// filters that touch blend into each other and a gap fades back to a clear screen.
/// </summary>
public static class AdvancedSchedule
{
    public const double FadeSeconds = Fade.DefaultSeconds;
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

    /// <summary>The level the filter asks for at <paramref name="now"/> — its flat level, or its curve.</summary>
    public static double LevelAt(Filter filter, TimeOnly now)
    {
        var elapsed = SecondsSince(filter.Start, now);
        return filter.LevelAt((int)Math.Clamp(elapsed / 60, 0, filter.LengthMinutes));
    }

    /// <summary>True when a fade is running or about to, so the app can check back more often.</summary>
    public static bool IsFading(IEnumerable<Filter> filters, TimeOnly now) =>
        filters.Any(filter =>
        {
            var weight = WeightOf(filter, now);
            if (weight is > 0 and < 1) return true;
            return SecondsUntil(filter.Start, now) <= FadeSeconds || SecondsUntil(filter.End, now) <= FadeSeconds;
        });

    static double WeightOf(Filter filter, TimeOnly now)
    {
        var length = filter.LengthMinutes * 60.0;
        var elapsed = SecondsSince(filter.Start, now);
        if (FadeSeconds <= 0) return elapsed <= length ? 1 : 0;

        // The 30 s before the start read as a negative elapsed time near the end of the day.
        var beforeStart = DaySeconds - elapsed;
        if (beforeStart <= FadeSeconds && elapsed > length) return 1 - beforeStart / FadeSeconds;

        if (elapsed <= length) return 1;

        var afterEnd = elapsed - length;
        return afterEnd < FadeSeconds ? 1 - afterEnd / FadeSeconds : 0;
    }

    static double SecondsSince(TimeOnly start, TimeOnly now) =>
        ((now.ToTimeSpan() - start.ToTimeSpan()).TotalSeconds % DaySeconds + DaySeconds) % DaySeconds;

    static double SecondsUntil(TimeOnly moment, TimeOnly now) => DaySeconds - SecondsSince(moment, now);
}
