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

    /// <summary>
    /// Test slider, 0–100: how light the black is, from pure black (0) to the e-ink look's charcoal (100, 15 % grey).
    /// </summary>
    public static double Black
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Changed?.Invoke();
        }
    } = 100;

    /// <summary>
    /// Test slider, 0–100: how dark the white is, from pure white (0) to a 70 % grey (100), darker than the e-ink
    /// look's old soft white (88 %).
    /// </summary>
    public static double White
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Test checkbox: plain black and white (greyscale from pure black to pure white), to compare with e-ink.</summary>
    public static bool Classic
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            Changed?.Invoke();
        }
    }

    /// <summary>The grey the black slider gives, 0–1.</summary>
    public static double InkGrey(double black) => MaxInk * Math.Clamp(black, 0, 100) / 100;

    /// <summary>The grey the white slider gives, 0–1.</summary>
    public static double PaperGrey(double white) => 1 - (1 - MinPaper) * Math.Clamp(white, 0, 100) / 100;

    public const double MaxInk = 0.15, MinPaper = 0.70;

    /// <summary>The look the test controls make: the picked look with its black and white replaced, or plain greyscale.</summary>
    public static EInkLook Shape(EInkLook look)
    {
        var ink = Classic ? 0 : InkGrey(Black);
        var paper = Classic ? 1 : PaperGrey(White);
        return look with { Ink = (ink, ink, ink), Paper = (paper, paper, paper), Low = 0, High = 1 };
    }

    /// <summary>What the screen shows for the picked look: its shape with the amber on top.</summary>
    public static float[] Matrix(EInkLook look) => TintMatrix.EInk(Shape(look), Amber);

    /// <summary>The look, the amber amount or a test control changed.</summary>
    public static event Action? Changed;
}
#endif
