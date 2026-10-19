#if DEBUG
using Amber.Tint;

namespace Amber;

/// <summary>
/// The e-ink experiment (test builds only): which look is picked, in Simple's chips or the tray, and how much Amber goes
/// on top of it. Kept in memory only, deliberately apart from <see cref="Settings"/>: the settings file is shared with the
/// installed Amber, which would throw all its settings away on a tint it doesn't know. Every run starts with no look and
/// no amber.
/// </summary>
public static class EInkExperiment
{
    /// <summary>The look replacing Simple's tint, or null for the normal tint.</summary>
    public static EInkLook? Look
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Changed?.Invoke();
        }
    }

    /// <summary>How much Amber covers the look, 0–100 (<see cref="TintMatrix.EInk(EInkLook, double)"/>). Shared by both looks.</summary>
    public static double Amber
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Changed?.Invoke();
        }
    }

    /// <summary>The look or the amber amount changed.</summary>
    public static event Action? Changed;
}
#endif
