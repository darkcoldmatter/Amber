using Amber.Tint;

namespace Amber;

/// <summary>
/// Turns "what the settings ask for" into "what the screen shows". Advanced mode eases in and out over
/// 30 s, so switching it on, or a filter's time arriving, never snaps. Simple mode and the live preview
/// are shown as they are — simple mode does its own fades from the schedule's transition setting.
/// </summary>
public sealed class TintPlan
{
    float[]? _lastTint;

    /// <summary>How far the advanced fade has got: 0 = clear screen, 1 = the full tint.</summary>
    public double Fade { get; private set; }

    /// <summary>True while a fade still has some way to go.</summary>
    public bool IsFading => Fade is > 0 and < 1;

    /// <param name="target">the tint the settings ask for, or null for none</param>
    /// <param name="eased">advanced mode: ease the change instead of applying it at once</param>
    /// <param name="seconds">time since the last step</param>
    public float[]? Next(float[]? target, bool eased, double seconds, double fadeSeconds = AdvancedSchedule.FadeSeconds)
    {
        if (!eased)
        {
            Fade = target is null ? 0 : 1;
            if (target is not null) _lastTint = target;
            return target;
        }

        if (target is not null) _lastTint = target;
        Fade = Amber.Fade.Step(Fade, target is not null, seconds, fadeSeconds);

        if (_lastTint is null || Fade <= 0) return null;
        return Fade >= 1 ? _lastTint : TintMatrix.Mix([(_lastTint, Fade)]);
    }
}
