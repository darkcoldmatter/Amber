namespace Amber.Tint;

public enum TintMode
{
    GoldenHour,
    Amber,
    DeepNight,
    /// <summary>A colour the user picks on the colour bar, kept as a hue (0–360) next to the mode.</summary>
    Custom,
}

public static class TintModeNames
{
    /// <summary>The name the user sees. These exact words are the owner's: don't reword them.</summary>
    public static string DisplayName(this TintMode mode) => mode switch
    {
        TintMode.GoldenHour => "Golden hour",
        TintMode.Amber => "Amber",
        TintMode.DeepNight => "Deep night",
        _ => "Custom",
    };
}
