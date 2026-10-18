using Amber.Tint;

namespace Amber;

/// <summary>
/// Turns "what the settings ask for" into "what the screen shows". When the tint comes or goes all at once — a mode
/// switched on or off — it eases over the plain fade (<see cref="Amber.Fade.DefaultSeconds"/>);
/// advanced filters fade at their edges by themselves. The live preview is shown as it is, and releasing it snaps
/// straight back to the real state.
/// </summary>
public sealed class TintPlan
{
    float[]? _lastTint;

    /// <summary>How far the plain fade has got: 0 = clear screen, 1 = the full tint.</summary>
    public double Fade { get; private set; }

    /// <summary>True while a fade still has some way to go.</summary>
    public bool IsFading => Fade is > 0 and < 1;

    /// <summary>Jumps straight to <paramref name="target"/>, with no fade — used when a preview ends.</summary>
    public void SnapTo(float[]? target)
    {
        _lastTint = target;
        Fade = target is null ? 0 : 1;
    }

    /// <param name="target">the tint the settings ask for, or null for none</param>
    /// <param name="eased">ease the change instead of applying it at once</param>
    /// <param name="seconds">time since the last step</param>
    public float[]? Next(float[]? target, bool eased, double seconds, double fadeSeconds = Amber.Fade.DefaultSeconds)
    {
        if (!eased)
        {
            Fade = target is null ? 0 : 1;
            if (target is not null) _lastTint = target;
            return target;
        }

        if (target is not null) _lastTint = target;
        // A fade that starts from rest begins with one small step: the time spent standing still before it doesn't
        // count, or switching on after a quiet minute would jump straight to the full tint.
        if (!IsFading) seconds = Math.Min(seconds, Amber.Fade.Tick(fadeSeconds).TotalSeconds);
        Fade = Amber.Fade.Step(Fade, target is not null, seconds, fadeSeconds);

        if (_lastTint is null || Fade <= 0) return null;
        return Fade >= 1 ? _lastTint : TintMatrix.Mix([(_lastTint, Fade)]);
    }
}
