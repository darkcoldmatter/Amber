using System.Windows.Input;
using Amber.Tint;

namespace Amber;

/// <summary>What a global shortcut does.</summary>
public enum ShortcutAction { Toggle, LevelUp, LevelDown, GoldenHour, Amber, DeepNight, Custom, EInk }

/// <summary>One combination to register and what it does.</summary>
public sealed record ShortcutBinding(ShortcutAction Action, Shortcut Keys);

/// <summary>
/// The global keyboard shortcuts, as saved in the settings file. On by default with Ctrl+Alt combinations; a shortcut
/// may be null (none). Immutable: an edit makes a new one, so <see cref="Settings"/> notices and Amber registers again.
/// </summary>
public sealed record ShortcutSettings
{
    const ModifierKeys CtrlAlt = ModifierKeys.Control | ModifierKeys.Alt;

    public bool Enabled { get; init; } = true;
    public Shortcut? Toggle { get; init; } = new(CtrlAlt, Key.A);
    public Shortcut? LevelUp { get; init; } = new(CtrlAlt, Key.OemPlus);
    public Shortcut? LevelDown { get; init; } = new(CtrlAlt, Key.OemMinus);
    public Shortcut? GoldenHour { get; init; } = new(CtrlAlt, Key.D1);
    public Shortcut? Amber { get; init; } = new(CtrlAlt, Key.D2);
    public Shortcut? DeepNight { get; init; } = new(CtrlAlt, Key.D3);
    public Shortcut? Custom { get; init; } = new(CtrlAlt, Key.D4);
    public Shortcut? EInk { get; init; } = new(CtrlAlt, Key.D5);

    public Shortcut? Get(ShortcutAction action) => action switch
    {
        ShortcutAction.Toggle => Toggle,
        ShortcutAction.LevelUp => LevelUp,
        ShortcutAction.LevelDown => LevelDown,
        ShortcutAction.GoldenHour => GoldenHour,
        ShortcutAction.Amber => Amber,
        ShortcutAction.DeepNight => DeepNight,
        ShortcutAction.Custom => Custom,
        _ => EInk,
    };

    /// <summary>A copy with one action's shortcut changed (null = none).</summary>
    public ShortcutSettings With(ShortcutAction action, Shortcut? keys) => action switch
    {
        ShortcutAction.Toggle => this with { Toggle = keys },
        ShortcutAction.LevelUp => this with { LevelUp = keys },
        ShortcutAction.LevelDown => this with { LevelDown = keys },
        ShortcutAction.GoldenHour => this with { GoldenHour = keys },
        ShortcutAction.Amber => this with { Amber = keys },
        ShortcutAction.DeepNight => this with { DeepNight = keys },
        ShortcutAction.Custom => this with { Custom = keys },
        _ => this with { EInk = keys },
    };

    /// <summary>Every combination that is set, whether or not shortcuts are switched on.</summary>
    public IEnumerable<ShortcutBinding> Bindings() =>
        Enum.GetValues<ShortcutAction>().Where(a => Get(a) is not null).Select(a => new ShortcutBinding(a, Get(a)!));

    /// <summary>Another action that already uses <paramref name="keys"/>, or null if there is none.</summary>
    public ShortcutAction? UsedBy(ShortcutAction action, Shortcut keys) =>
        Bindings().FirstOrDefault(b => b.Action != action && b.Keys == keys)?.Action;

    /// <summary>Whether the action picks a tint: the four <see cref="TintOf"/> tints and E-ink, after them.</summary>
    public static bool PicksTint(ShortcutAction action) => action == ShortcutAction.EInk || TintOf(action) is not null;

    /// <summary>The tint a tint shortcut picks, or null for the others (E-ink isn't a <see cref="TintMode"/>).</summary>
    public static TintMode? TintOf(ShortcutAction action) => action switch
    {
        ShortcutAction.GoldenHour => TintMode.GoldenHour,
        ShortcutAction.Amber => TintMode.Amber,
        ShortcutAction.DeepNight => TintMode.DeepNight,
        ShortcutAction.Custom => TintMode.Custom,
        _ => null,
    };

    /// <summary>The words Settings uses for an action, also in "Already used for …".</summary>
    public static string Label(ShortcutAction action) => action switch
    {
        ShortcutAction.Toggle => "Turn on / off",
        ShortcutAction.LevelUp => "Level up",
        ShortcutAction.LevelDown => "Level down",
        ShortcutAction.GoldenHour => TintMode.GoldenHour.DisplayName(),
        ShortcutAction.Amber => TintMode.Amber.DisplayName(),
        ShortcutAction.DeepNight => TintMode.DeepNight.DisplayName(),
        ShortcutAction.Custom => TintMode.Custom.DisplayName(),
        _ => "E-ink",
    };
}

/// <summary>What a pressed shortcut does to the settings. The screen, the window and the saved file follow from there.</summary>
public static class ShortcutCommands
{
    /// <summary>How far one press (or one repeat while held) of level up or down moves the level.</summary>
    public const int LevelStep = 2;

    /// <summary>
    /// On/off acts on the mode you are in, exactly like the tray. The level and tint shortcuts belong to Simple: they do
    /// nothing while Advanced is the current mode, and never switch the tint on (while OFF they just change the setting).
    /// </summary>
    public static void Run(ShortcutAction action, Settings settings)
    {
        if (action == ShortcutAction.Toggle)
        {
            if (settings.IsAdvanced) settings.AdvancedIsOn = !settings.AdvancedIsOn;
            else settings.IsOn = !settings.IsOn;
            return;
        }
        if (settings.IsAdvanced) return;

        switch (action)
        {
            case ShortcutAction.LevelUp: settings.Level = Step(settings.Level, +LevelStep); break;
            case ShortcutAction.LevelDown: settings.Level = Step(settings.Level, -LevelStep); break;
            case ShortcutAction.EInk: settings.EInk = true; break;
            default:
                settings.Mode = ShortcutSettings.TintOf(action)!.Value;
                settings.EInk = false;
                break;
        }
    }

    /// <summary>
    /// The level moved by <paramref name="by"/> whole percents, kept within 0–100. It's rounded first (50.4 + 2 is 52)
    /// and keeps its oddness, so 37 goes to 39 or 35; only the ends clip (99 + 2 is 100, 1 − 2 is 0).
    /// </summary>
    public static double Step(double level, int by) => Math.Clamp(Math.Round(level, MidpointRounding.AwayFromZero) + by, 0, 100);
}

public enum CaptureKind
{
    /// <summary>A lone Ctrl, Alt, Shift or Win: wait for the rest.</summary>
    Wait,
    /// <summary>Esc: leave the shortcut as it was.</summary>
    Cancel,
    /// <summary>Backspace or Delete: no shortcut.</summary>
    Clear,
    /// <summary>Not usable (<see cref="CaptureResult.Problem"/> says why): keep listening.</summary>
    Refused,
    /// <summary>A usable combination: <see cref="CaptureResult.Keys"/>.</summary>
    Done,
}

public enum CaptureProblem { None, NeedsModifier, KeyNotAllowed }

public sealed record CaptureResult(CaptureKind Kind, Shortcut? Keys = null, CaptureProblem Problem = CaptureProblem.None);

/// <summary>Turns a key press in Settings, while a shortcut pill is listening, into a new shortcut (or not).</summary>
public static class ShortcutCapture
{
    /// <summary><paramref name="key"/> is the real key (for Alt combinations WPF reports Key.System; pass its SystemKey).</summary>
    public static CaptureResult Read(Key key, ModifierKeys modifiers)
    {
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.None)
            return new(CaptureKind.Wait);
        if (modifiers == ModifierKeys.None && key == Key.Escape) return new(CaptureKind.Cancel);
        if (modifiers == ModifierKeys.None && key is Key.Back or Key.Delete) return new(CaptureKind.Clear);

        switch (Shortcut.Check(modifiers, key))
        {
            case ShortcutProblem.KeyNotAllowed: return new(CaptureKind.Refused, Problem: CaptureProblem.KeyNotAllowed);
            case ShortcutProblem.NeedsModifier: return new(CaptureKind.Refused, Problem: CaptureProblem.NeedsModifier);
        }
        return new(CaptureKind.Done, new Shortcut(modifiers, key));
    }

    /// <summary>The short line under a row when a press was refused.</summary>
    public static string Message(CaptureProblem problem) => problem switch
    {
        CaptureProblem.NeedsModifier => "Add Ctrl, Alt or Win",
        CaptureProblem.KeyNotAllowed => "Use a letter, a digit, =, - or F1–F12",
        _ => "",
    };
}
