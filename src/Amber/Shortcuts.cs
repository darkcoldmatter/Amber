using System.Text.Json.Serialization;
using System.Windows.Input;
using Amber.Tint;

namespace Amber;

/// <summary>What a global shortcut does. <see cref="LevelJump"/> is ten shortcuts: its modifiers with 1…9 and 0.</summary>
public enum ShortcutAction { Toggle, LevelUp, LevelDown, LevelJump, GoldenHour, Amber, DeepNight, Custom }

/// <summary>One combination to register: what it does and, for a jump, the level it jumps to (10–100).</summary>
public sealed record ShortcutBinding(ShortcutAction Action, Shortcut Keys, int Level = 0);

/// <summary>
/// The global keyboard shortcuts, as saved in the settings file. On by default with Ctrl+Alt combinations; a shortcut
/// may be null (none). Immutable: an edit makes a new one, so <see cref="Settings"/> notices and Amber registers again.
/// </summary>
public sealed record ShortcutSettings
{
    const ModifierKeys CtrlAlt = ModifierKeys.Control | ModifierKeys.Alt;
    const ModifierKeys CtrlAltShift = CtrlAlt | ModifierKeys.Shift;

    public bool Enabled { get; init; } = true;
    public Shortcut? Toggle { get; init; } = new(CtrlAlt, Key.A);
    public Shortcut? LevelUp { get; init; } = new(CtrlAlt, Key.OemPlus);
    public Shortcut? LevelDown { get; init; } = new(CtrlAlt, Key.OemMinus);

    /// <summary>The modifiers that, with 1…9 and 0, jump to 10…90 and 100 %; none (null in the file) turns all ten off.</summary>
    [JsonConverter(typeof(ModifiersJsonConverter))]
    public ModifierKeys LevelJump { get; init; } = CtrlAlt;

    public Shortcut? GoldenHour { get; init; } = new(CtrlAltShift, Key.D1);
    public Shortcut? Amber { get; init; } = new(CtrlAltShift, Key.D2);
    public Shortcut? DeepNight { get; init; } = new(CtrlAltShift, Key.D3);
    public Shortcut? Custom { get; init; } = new(CtrlAltShift, Key.D4);

    /// <summary>The digit keys of the jump row, in order: 1 jumps to 10 %, … 9 to 90 %, 0 to 100 %.</summary>
    public static readonly Key[] JumpKeys = [Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9, Key.D0];

    /// <summary>The shortcut of one action; for the jump row, the one that jumps to 10 % (its modifiers + 1).</summary>
    public Shortcut? Get(ShortcutAction action) => action switch
    {
        ShortcutAction.Toggle => Toggle,
        ShortcutAction.LevelUp => LevelUp,
        ShortcutAction.LevelDown => LevelDown,
        ShortcutAction.LevelJump => LevelJump == ModifierKeys.None ? null : new(LevelJump, Key.D1),
        ShortcutAction.GoldenHour => GoldenHour,
        ShortcutAction.Amber => Amber,
        ShortcutAction.DeepNight => DeepNight,
        _ => Custom,
    };

    /// <summary>A copy with one action's shortcut changed (null = none). The jump row takes only the modifiers.</summary>
    public ShortcutSettings With(ShortcutAction action, Shortcut? keys) => action switch
    {
        ShortcutAction.Toggle => this with { Toggle = keys },
        ShortcutAction.LevelUp => this with { LevelUp = keys },
        ShortcutAction.LevelDown => this with { LevelDown = keys },
        ShortcutAction.LevelJump => this with { LevelJump = keys?.Modifiers ?? ModifierKeys.None },
        ShortcutAction.GoldenHour => this with { GoldenHour = keys },
        ShortcutAction.Amber => this with { Amber = keys },
        ShortcutAction.DeepNight => this with { DeepNight = keys },
        _ => this with { Custom = keys },
    };

    /// <summary>Every combination that is set, whether or not shortcuts are switched on.</summary>
    public IEnumerable<ShortcutBinding> Bindings()
    {
        foreach (var action in Enum.GetValues<ShortcutAction>())
        {
            if (action == ShortcutAction.LevelJump)
            {
                if (LevelJump == ModifierKeys.None) continue;
                for (var i = 0; i < JumpKeys.Length; i++)
                    yield return new(action, new Shortcut(LevelJump, JumpKeys[i]), (i + 1) * 10);
            }
            else if (Get(action) is { } keys)
                yield return new(action, keys);
        }
    }

    /// <summary>
    /// Another action that already uses a combination <paramref name="action"/> would get with <paramref name="keys"/>,
    /// or null if there is none. For the jump row, any of its ten combinations counts.
    /// </summary>
    public ShortcutAction? UsedBy(ShortcutAction action, Shortcut keys)
    {
        var wanted = With(action, keys).Bindings().Where(b => b.Action == action).Select(b => b.Keys).ToHashSet();
        return Bindings().FirstOrDefault(b => b.Action != action && wanted.Contains(b.Keys))?.Action;
    }

    /// <summary>The words Settings uses for an action, also in "Already used for …".</summary>
    public static string Label(ShortcutAction action) => action switch
    {
        ShortcutAction.Toggle => "Turn on / off",
        ShortcutAction.LevelUp => "Level up",
        ShortcutAction.LevelDown => "Level down",
        ShortcutAction.LevelJump => "Jump to level 10–100%",
        ShortcutAction.GoldenHour => TintMode.GoldenHour.DisplayName(),
        ShortcutAction.Amber => TintMode.Amber.DisplayName(),
        ShortcutAction.DeepNight => TintMode.DeepNight.DisplayName(),
        _ => TintMode.Custom.DisplayName(),
    };
}

/// <summary>What a pressed shortcut does to the settings. The screen, the window and the saved file follow from there.</summary>
public static class ShortcutCommands
{
    /// <summary>
    /// On/off acts on the mode you are in, exactly like the tray. The level and tint shortcuts belong to Simple: they do
    /// nothing while Advanced is the current mode, and never switch the tint on (while OFF they just change the setting).
    /// </summary>
    public static void Run(ShortcutAction action, int jumpLevel, Settings settings)
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
            case ShortcutAction.LevelUp: settings.Level = Step(settings.Level, +1); break;
            case ShortcutAction.LevelDown: settings.Level = Step(settings.Level, -1); break;
            case ShortcutAction.LevelJump: settings.Level = Math.Clamp(jumpLevel, 0, 100); break;
            case ShortcutAction.GoldenHour: settings.Mode = TintMode.GoldenHour; break;
            case ShortcutAction.Amber: settings.Mode = TintMode.Amber; break;
            case ShortcutAction.DeepNight: settings.Mode = TintMode.DeepNight; break;
            case ShortcutAction.Custom: settings.Mode = TintMode.Custom; break;
        }
    }

    /// <summary>One whole percent up or down from the level (rounded first, so 50.4 + 1 is 51), kept within 0–100.</summary>
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

public enum CaptureProblem { None, NeedsModifier, KeyNotAllowed, NeedsDigit }

public sealed record CaptureResult(CaptureKind Kind, Shortcut? Keys = null, CaptureProblem Problem = CaptureProblem.None);

/// <summary>Turns a key press in Settings, while a shortcut pill is listening, into a new shortcut (or not).</summary>
public static class ShortcutCapture
{
    /// <summary>
    /// <paramref name="key"/> is the real key (for Alt combinations WPF reports Key.System; pass its SystemKey).
    /// The jump row (<paramref name="digitOnly"/>) wants its modifiers with a digit.
    /// </summary>
    public static CaptureResult Read(Key key, ModifierKeys modifiers, bool digitOnly)
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
        if (digitOnly && key is not (>= Key.D0 and <= Key.D9)) return new(CaptureKind.Refused, Problem: CaptureProblem.NeedsDigit);
        return new(CaptureKind.Done, new Shortcut(modifiers, key));
    }

    /// <summary>The short line under a row when a press was refused.</summary>
    public static string Message(CaptureProblem problem) => problem switch
    {
        CaptureProblem.NeedsModifier => "Add Ctrl, Alt or Win",
        CaptureProblem.KeyNotAllowed => "Use a letter, a digit, =, - or F1–F12",
        CaptureProblem.NeedsDigit => "Hold the keys and press a digit",
        _ => "",
    };
}
