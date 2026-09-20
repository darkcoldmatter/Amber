namespace Amber;

/// <summary>Works out how strong the tint should be at a moment in time.</summary>
public static class Schedule
{
    /// <summary>A transition of 0 minutes still fades, just quickly.</summary>
    public const double InstantFadeSeconds = 10;

    const double DaySeconds = 24 * 60 * 60;

    /// <summary>
    /// The level to show at <paramref name="now"/>, fading in at the start of the range and out at its end.
    /// Returns 0 outside the range.
    /// </summary>
    public static double LevelAt(TimeOnly now, TimeOnly start, TimeOnly end, double level, int transitionMinutes)
    {
        var length = ScheduleRange.Length(ScheduleRange.Minutes(start), ScheduleRange.Minutes(end)) * 60.0;
        var elapsed = ((now.ToTimeSpan() - start.ToTimeSpan()).TotalSeconds % DaySeconds + DaySeconds) % DaySeconds;
        if (elapsed >= length) return 0;

        // Never let the two fades meet in the middle.
        var fade = Math.Min(transitionMinutes > 0 ? transitionMinutes * 60.0 : InstantFadeSeconds, length / 2);
        if (elapsed < fade) return level * elapsed / fade;

        var remaining = length - elapsed;
        return remaining < fade ? level * remaining / fade : level;
    }

    /// <summary>True while the level is on its way up or down, so the app can check back more often.</summary>
    public static bool IsFading(TimeOnly now, TimeOnly start, TimeOnly end, int transitionMinutes)
    {
        var length = ScheduleRange.Length(ScheduleRange.Minutes(start), ScheduleRange.Minutes(end)) * 60.0;
        var elapsed = ((now.ToTimeSpan() - start.ToTimeSpan()).TotalSeconds % DaySeconds + DaySeconds) % DaySeconds;
        var fade = Math.Min(transitionMinutes > 0 ? transitionMinutes * 60.0 : InstantFadeSeconds, length / 2);

        if (elapsed >= length) return DaySeconds - elapsed < fade; // about to start
        return elapsed < fade || length - elapsed < fade;
    }
}
