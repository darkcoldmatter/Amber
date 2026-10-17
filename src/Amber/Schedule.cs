namespace Amber;

/// <summary>Works out how strong the tint should be at a moment in time.</summary>
public static class Schedule
{
    /// <summary>A transition of 0 ("Off") falls back to the plain fade, <see cref="Fade.DefaultSeconds"/>.</summary>
    public const double InstantFadeSeconds = Fade.DefaultSeconds;

    const double DaySeconds = 24 * 60 * 60;

    /// <summary>
    /// How much of the tint to show at <paramref name="now"/>: 0 outside the range, 1 inside it,
    /// and in between while it fades in at the start and out at the end.
    /// </summary>
    public static double StrengthAt(TimeOnly now, TimeOnly start, TimeOnly end, int transitionSeconds)
    {
        var length = ScheduleRange.Length(ScheduleRange.Minutes(start), ScheduleRange.Minutes(end)) * 60.0;
        if (length >= DaySeconds) return 1;   // a whole-day range has no edges to fade at
        var elapsed = ((now.ToTimeSpan() - start.ToTimeSpan()).TotalSeconds % DaySeconds + DaySeconds) % DaySeconds;
        if (elapsed >= length) return 0;

        // Never let the two fades meet in the middle.
        var fade = Math.Min(transitionSeconds > 0 ? transitionSeconds : InstantFadeSeconds, length / 2);
        if (elapsed < fade) return elapsed / fade;

        var remaining = length - elapsed;
        return remaining < fade ? remaining / fade : 1;
    }

    /// <summary>
    /// True while the level is on its way up or down, or starts moving within <paramref name="lookAhead"/>. The app
    /// passes the time to its next idle check, so it is already checking often when a fade begins.
    /// </summary>
    public static bool IsFading(TimeOnly now, TimeOnly start, TimeOnly end, int transitionSeconds, TimeSpan lookAhead)
    {
        var length = ScheduleRange.Length(ScheduleRange.Minutes(start), ScheduleRange.Minutes(end)) * 60.0;
        if (length >= DaySeconds) return false;   // a whole-day range has no edges to fade at
        var elapsed = ((now.ToTimeSpan() - start.ToTimeSpan()).TotalSeconds % DaySeconds + DaySeconds) % DaySeconds;
        var fade = Math.Min(transitionSeconds > 0 ? transitionSeconds : InstantFadeSeconds, length / 2);
        var ahead = lookAhead.TotalSeconds;

        if (elapsed >= length) return DaySeconds - elapsed <= ahead;   // the fade-in begins soon
        return elapsed < fade || length - elapsed <= fade + ahead;     // fading in, or fading out (or about to)
    }
}
