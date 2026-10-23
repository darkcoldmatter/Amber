using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace Amber;

/// <summary>
/// One global keyboard shortcut: modifiers (Ctrl, Alt, Shift, Win) and one key. Saved as readable text, "Ctrl+Alt+A".
/// Only letters, digits, = , - and F1–F12, and always with Ctrl, Alt or Win: a lone key or Shift+key would take over typing.
/// </summary>
[JsonConverter(typeof(ShortcutJsonConverter))]
public sealed record Shortcut(ModifierKeys Modifiers, Key Key)
{
    /// <summary>Why a pressed combination can't be a shortcut, or <see cref="ShortcutProblem.None"/> if it can.</summary>
    public static ShortcutProblem Check(ModifierKeys modifiers, Key key) =>
        KeyName(key) is null ? ShortcutProblem.KeyNotAllowed
        : (modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0 ? ShortcutProblem.NeedsModifier
        : ShortcutProblem.None;

    /// <summary>"Ctrl+Alt+A", "ctrl + alt + =", any order of modifiers; null for anything that isn't a valid shortcut.</summary>
    public static Shortcut? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parts = text.Split('+').Select(p => p.Trim()).ToArray();
        if (ParseModifiers(parts[..^1]) is not { } modifiers || ParseKey(parts[^1]) is not { } key) return null;
        return Check(modifiers, key) == ShortcutProblem.None ? new Shortcut(modifiers, key) : null;
    }

    static ModifierKeys? ParseModifiers(string[] parts)
    {
        var modifiers = ModifierKeys.None;
        foreach (var part in parts)
        {
            var modifier = part.ToLowerInvariant() switch
            {
                "ctrl" => ModifierKeys.Control,
                "alt" => ModifierKeys.Alt,
                "shift" => ModifierKeys.Shift,
                "win" => ModifierKeys.Windows,
                _ => ModifierKeys.None,
            };
            if (modifier == ModifierKeys.None || (modifiers & modifier) != 0) return null;
            modifiers |= modifier;
        }
        return modifiers;
    }

    static Key? ParseKey(string text)
    {
        var upper = text.ToUpperInvariant();
        if (upper.Length == 1 && upper[0] is >= 'A' and <= 'Z') return Key.A + (upper[0] - 'A');
        if (upper.Length == 1 && upper[0] is >= '0' and <= '9') return Key.D0 + (upper[0] - '0');
        if (upper == "=") return Key.OemPlus;
        if (upper == "-") return Key.OemMinus;
        if (upper.Length is 2 or 3 && upper[0] == 'F' && int.TryParse(upper[1..], out var n) && n is >= 1 and <= 12 && upper[1] != '0')
            return Key.F1 + (n - 1);
        return null;
    }

    /// <summary>The key as written in a shortcut, or null if it can't be one.</summary>
    public static string? KeyName(Key key) => key switch
    {
        >= Key.A and <= Key.Z => ((char)('A' + (key - Key.A))).ToString(),
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.F1 and <= Key.F12 => $"F{key - Key.F1 + 1}",
        Key.OemPlus => "=",
        Key.OemMinus => "-",
        _ => null,
    };

    /// <summary>Modifiers in a fixed order: Ctrl, Alt, Shift, Win.</summary>
    public static IEnumerable<string> ModifierNames(ModifierKeys modifiers)
    {
        if (modifiers.HasFlag(ModifierKeys.Control)) yield return "Ctrl";
        if (modifiers.HasFlag(ModifierKeys.Alt)) yield return "Alt";
        if (modifiers.HasFlag(ModifierKeys.Shift)) yield return "Shift";
        if (modifiers.HasFlag(ModifierKeys.Windows)) yield return "Win";
    }

    /// <summary>The saved form, "Ctrl+Alt+A"; also what the tray menu shows.</summary>
    public override string ToString() => string.Join("+", ModifierNames(Modifiers).Append(KeyName(Key)));

    /// <summary>What a Settings pill shows: "Ctrl + Alt + A".</summary>
    public string Display => string.Join(" + ", ModifierNames(Modifiers).Append(KeyName(Key)));
}

public enum ShortcutProblem { None, NeedsModifier, KeyNotAllowed }

/// <summary>Reads and writes a shortcut as its text. Never throws: an unreadable value loads as "no shortcut".</summary>
public sealed class ShortcutJsonConverter : JsonConverter<Shortcut>
{
    public override Shortcut? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return Shortcut.Parse(reader.GetString());
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, Shortcut value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
