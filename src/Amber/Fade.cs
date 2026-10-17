namespace Amber;

/// <summary>
/// The plain fade: how far a tint has faded in, 0 to 1, when it comes or goes all at once — a mode switched on or
/// off, or a schedule whose transition is "Off" — and how often any fade is stepped. Advanced filters fade at their
/// edges by themselves, in <see cref="AdvancedSchedule"/>.
/// </summary>
public static class Fade
{
    /// <summary>
    /// How long a plain fade takes, in seconds — on/off in both modes, and a schedule transition set to "Off".
    /// 0 means no fade at all (the owner's choice for now).
    /// </summary>
    public const double DefaultSeconds = 0;


    /// <param name="current">where the fade is now</param>
    /// <param name="wanted">true while there is a tint to show</param>
    /// <param name="seconds">time since the last step</param>
    /// <param name="fadeSeconds">how long a full fade takes</param>
    public static double Step(double current, bool wanted, double seconds, double fadeSeconds)
    {
        var change = fadeSeconds <= 0 ? 1 : Math.Max(seconds, 0) / fadeSeconds;
        return Math.Clamp(wanted ? current + change : current - change, 0, 1);
    }

    /// <summary>
    /// How often to step a fade of this length: about 1 % of it at a time, so it looks smooth, but never more often
    /// than every 100 ms and never less often than every 500 ms.
    /// </summary>
    public static TimeSpan Tick(double fadeSeconds) => TimeSpan.FromSeconds(Math.Clamp(fadeSeconds / 100, 0.1, 0.5));
}
