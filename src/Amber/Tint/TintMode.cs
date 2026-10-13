namespace Amber.Tint;

public enum TintMode
{
    GoldenHour,
    Amber,
    DeepNight,
}

public static class TintModeNames
{
    /// <summary>The name the user sees. These exact words are the owner's: don't reword them.</summary>
    public static string DisplayName(this TintMode mode) => mode switch
    {
        TintMode.GoldenHour => "Golden hour",
        TintMode.Amber => "Amber",
        _ => "Deep night",
    };
}
