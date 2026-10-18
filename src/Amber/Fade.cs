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
    /// A quick 0.2 s (the owner's choice), so the tint glides in and out instead of switching hard.
    /// </summary>
    public const double DefaultSeconds = 0.2;


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
    /// than once a screen frame (1/60 s) and never less often than every 500 ms. A 0.2 s fade can't step by 1 %, so it
    /// steps every frame instead, which looks just as smooth.
    /// </summary>
    public static TimeSpan Tick(double fadeSeconds) => TimeSpan.FromSeconds(Math.Clamp(fadeSeconds / 100, 1.0 / 60, 0.5));
}
