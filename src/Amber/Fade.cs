namespace Amber;

/// <summary>
/// How far a tint has faded in, 0 to 1. Advanced mode uses this so the screen eases in when a filter
/// starts (or when you switch the mode on) and eases out when the tint goes away.
/// </summary>
public static class Fade
{
    /// <summary>
    /// How long a plain fade takes, in seconds — on/off in both modes, and between filters.
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
}
