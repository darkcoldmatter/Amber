using System.IO;
using System.Reflection;

namespace Amber;

/// <summary>
/// What's new in a version, from <c>ReleaseNotes.md</c> (shipped inside Amber): the version named by its first
/// <c>## x.y.z</c> line, and its <c>- </c> or <c>* </c> bullets as plain text. Every other line is ignored.
/// </summary>
public sealed record ReleaseNotes(string? Version, IReadOnlyList<string> Bullets)
{
    public static ReleaseNotes Parse(string text)
    {
        string? version = null;
        var bullets = new List<string>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (version is null && line.StartsWith("## ")) version = line[3..].Trim();
            else if (line.StartsWith("- ") || line.StartsWith("* "))
                if (line[2..].Replace("**", "").Trim() is { Length: > 0 } bullet) bullets.Add(bullet);
        }
        return new(version, bullets);
    }

    /// <summary>The notes built into this copy of Amber (an embedded resource); none if it's missing.</summary>
    public static ReleaseNotes Bundled()
    {
        using var stream = typeof(ReleaseNotes).Assembly.GetManifestResourceStream("Amber.ReleaseNotes.md");
        return stream is null ? new(null, []) : Parse(new StreamReader(stream).ReadToEnd());
    }
}

/// <summary>What to do about the "New in this version" window when Amber starts.</summary>
public enum NotesAction
{
    Nothing,
    /// <summary>Show the window the next time the main window is visible.</summary>
    Show,
    /// <summary>A fresh install: nothing is new to this user, so count this version's notes as seen without showing them.</summary>
    MarkSilently,
}

public static class WhatsNew
{
    /// <summary>This copy's version as About shows it, "1.0.2", without the build's commit id that follows a "+".</summary>
    public static string ThisVersion { get; } =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "";

    /// <summary>
    /// After an update, an installed Amber shows this version's notes once. A fresh install (no settings file yet)
    /// shows nothing and remembers the version as seen. Notes written for another version are never shown.
    /// </summary>
    /// <param name="hadSettingsFile">whether a settings file was there when Amber started (<see cref="Settings.HadFile"/>)</param>
    /// <param name="shownVersion">the version whose notes were last closed (<see cref="Settings.NotesShownVersion"/>)</param>
    public static NotesAction Decide(bool installed, bool hadSettingsFile, ReleaseNotes notes, string appVersion, string? shownVersion)
    {
        if (!installed) return NotesAction.Nothing;
        if (!hadSettingsFile) return NotesAction.MarkSilently;
        if (shownVersion == appVersion || notes.Version != appVersion || notes.Bullets.Count == 0) return NotesAction.Nothing;
        return NotesAction.Show;
    }
}
