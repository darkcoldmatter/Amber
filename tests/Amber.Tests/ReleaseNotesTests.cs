namespace Amber.Tests;

public class ReleaseNotesTests
{
    static readonly ReleaseNotes Notes102 = new("1.0.2", ["Smoother switching"]);

    [Fact]
    public void The_first_heading_names_the_version_and_dashed_lines_are_the_bullets()
    {
        var notes = ReleaseNotes.Parse("## 1.0.2\n\n- Switching the tint on or off now glides in and out\n- A soft wave of dots\n");

        Assert.Equal("1.0.2", notes.Version);
        Assert.Equal(["Switching the tint on or off now glides in and out", "A soft wave of dots"], notes.Bullets);
    }

    [Fact]
    public void Star_bullets_count_too_and_bold_markers_are_dropped()
    {
        var notes = ReleaseNotes.Parse("## 1.0.2\r\n* **New:** a wave of dots\r\n  - Fades are **smoother**\r\n");

        Assert.Equal(["New: a wave of dots", "Fades are smoother"], notes.Bullets);
    }

    [Fact]
    public void Other_lines_are_ignored()
    {
        var notes = ReleaseNotes.Parse("""
            # Amber
            Some words about this release.
            ## 1.0.2
            -not a bullet
            - A bullet
            ## 1.0.1
            """);

        Assert.Equal("1.0.2", notes.Version);
        Assert.Equal(["A bullet"], notes.Bullets);
    }

    [Fact]
    public void A_file_without_bullets_or_a_version_has_none()
    {
        Assert.Empty(ReleaseNotes.Parse("## 1.0.2\n\nNothing new.").Bullets);
        Assert.Null(ReleaseNotes.Parse("- A bullet").Version);
        Assert.Empty(ReleaseNotes.Parse("").Bullets);
    }

    [Fact]
    public void The_notes_shipped_inside_Amber_can_be_read()
    {
        var notes = ReleaseNotes.Bundled();

        Assert.Matches(@"^\d+\.\d+\.\d+", notes.Version);
        Assert.NotEmpty(notes.Bullets);
    }

    [Fact]
    public void An_update_shows_the_notes_of_the_version_it_became()
    {
        Assert.Equal(NotesAction.Show, WhatsNew.Decide(installed: true, hadSettingsFile: true, Notes102, "1.0.2", shownVersion: null));
        Assert.Equal(NotesAction.Show, WhatsNew.Decide(installed: true, hadSettingsFile: true, Notes102, "1.0.2", shownVersion: "1.0.1"));
    }

    [Fact]
    public void Notes_show_only_once()
    {
        Assert.Equal(NotesAction.Nothing, WhatsNew.Decide(installed: true, hadSettingsFile: true, Notes102, "1.0.2", shownVersion: "1.0.2"));
    }

    [Fact]
    public void A_fresh_install_counts_the_notes_as_seen_without_showing_them()
    {
        Assert.Equal(NotesAction.MarkSilently, WhatsNew.Decide(installed: true, hadSettingsFile: false, Notes102, "1.0.2", shownVersion: null));
    }

    [Fact]
    public void Notes_for_another_version_or_without_bullets_never_show()
    {
        Assert.Equal(NotesAction.Nothing, WhatsNew.Decide(installed: true, hadSettingsFile: true, Notes102, "1.0.3", shownVersion: "1.0.2"));
        Assert.Equal(NotesAction.Nothing, WhatsNew.Decide(installed: true, hadSettingsFile: true, new("1.0.2", []), "1.0.2", shownVersion: null));
        Assert.Equal(NotesAction.Nothing, WhatsNew.Decide(installed: true, hadSettingsFile: true, new(null, ["A bullet"]), "1.0.2", shownVersion: null));
    }

    [Fact]
    public void A_copy_run_from_a_build_folder_never_shows_or_marks_them()
    {
        Assert.Equal(NotesAction.Nothing, WhatsNew.Decide(installed: false, hadSettingsFile: true, Notes102, "1.0.2", shownVersion: null));
        Assert.Equal(NotesAction.Nothing, WhatsNew.Decide(installed: false, hadSettingsFile: false, Notes102, "1.0.2", shownVersion: null));
    }
}
