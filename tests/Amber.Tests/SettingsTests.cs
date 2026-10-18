using System.ComponentModel;
using Amber.Tint;

namespace Amber.Tests;

public class SettingsTests : IDisposable
{
    readonly string _folder = Path.Combine(Path.GetTempPath(), $"amber-tests-{Guid.NewGuid():N}");
    string SettingsPath => Path.Combine(_folder, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void Saved_settings_load_back()
    {
        new Settings
        {
            IsOn = true,
            Mode = TintMode.DeepNight,
            Level = 73.5,
            ScheduleEnabled = true,
            ScheduleStart = new TimeOnly(22, 15),
            ScheduleEnd = new TimeOnly(6, 45),
        }.Save(SettingsPath);

        var loaded = Settings.Load(SettingsPath);

        Assert.True(loaded.IsOn);
        Assert.Equal(TintMode.DeepNight, loaded.Mode);
        Assert.Equal(73.5, loaded.Level);
        Assert.True(loaded.ScheduleEnabled);
        Assert.Equal(new TimeOnly(22, 15), loaded.ScheduleStart);
        Assert.Equal(new TimeOnly(6, 45), loaded.ScheduleEnd);
    }

    [Fact]
    public void Advanced_filters_are_saved_and_read_back()
    {
        var settings = new Settings { IsAdvanced = true, AdvancedIsOn = true };
        var filter = new Filter { Mode = TintMode.DeepNight, Start = new TimeOnly(8, 0), End = new TimeOnly(16, 0), Level = 70 };
        filter.ResetCurve();
        filter.Curve = CurveMath.Add(filter.Curve, 180, 90, filter.LengthMinutes);
        settings.Filters.Add(filter);
        settings.Save(SettingsPath);

        var loaded = Settings.Load(SettingsPath);

        Assert.True(loaded.IsAdvanced);
        Assert.True(loaded.AdvancedIsOn);
        var read = Assert.Single(loaded.Filters);
        Assert.Equal(TintMode.DeepNight, read.Mode);
        Assert.Equal(new TimeOnly(8, 0), read.Start);
        Assert.Equal(new TimeOnly(16, 0), read.End);
        Assert.Equal(70, read.Level);
        Assert.Equal([new CurvePoint(0, 70), new CurvePoint(180, 90), new CurvePoint(480, 70)], read.Curve);
    }

    [Fact]
    public void Closed_intro_and_used_hint_are_remembered()
    {
        new Settings { AdvancedIntroClosed = true, FilterHintDone = true }.Save(SettingsPath);

        var loaded = Settings.Load(SettingsPath);

        Assert.True(loaded.AdvancedIntroClosed);
        Assert.True(loaded.FilterHintDone);
    }

    [Fact]
    public void A_saved_curve_that_stops_short_reaches_its_range_end_when_loaded()
    {
        // The owner's own Amber filter: 21:45 to midnight is 135 minutes, but its curve was saved stopping at 60.
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, """
            { "Filters": [ { "Mode": "Amber", "Start": "21:45:00", "End": "00:00:00", "Level": 50, "CurveEnabled": true,
              "Curve": [ { "Minutes": 0, "Level": 1 }, { "Minutes": 15, "Level": 1 }, { "Minutes": 30, "Level": 100 }, { "Minutes": 60, "Level": 100 } ] } ] }
            """);

        var curve = Assert.Single(Settings.Load(SettingsPath).Filters).Curve;

        Assert.Equal([new CurvePoint(0, 1), new CurvePoint(15, 1), new CurvePoint(30, 100), new CurvePoint(60, 100), new CurvePoint(135, 100)], curve);
    }

    [Fact]
    public void The_file_says_which_layout_it_was_written_in()
    {
        new Settings().Save(SettingsPath);

        Assert.StartsWith("{\n  \"SettingsVersion\": 1,", File.ReadAllText(SettingsPath).ReplaceLineEndings("\n"));
        Assert.Equal(Settings.CurrentVersion, Settings.Load(SettingsPath).SettingsVersion);
    }

    [Fact]
    public void A_skipped_update_is_remembered()
    {
        new Settings { SkippedVersion = "1.1.0" }.Save(SettingsPath);

        Assert.Equal("1.1.0", Settings.Load(SettingsPath).SkippedVersion);
    }

    [Fact]
    public void The_version_whose_notes_were_shown_is_remembered()
    {
        new Settings { NotesShownVersion = "1.0.2" }.Save(SettingsPath);

        Assert.Equal("1.0.2", Settings.Load(SettingsPath).NotesShownVersion);
        Assert.DoesNotContain(nameof(Settings.HadFile), File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void Loading_tells_whether_there_was_a_settings_file()
    {
        // A fresh install has no file, and no folder either.
        Assert.False(Settings.Load(SettingsPath).HadFile);

        new Settings().Save(SettingsPath);
        Assert.True(Settings.Load(SettingsPath).HadFile);

        // Unreadable, but there: Amber has run here before.
        File.WriteAllText(SettingsPath, "{ not json");
        Assert.True(Settings.Load(SettingsPath).HadFile);
    }

    [Fact]
    public void A_file_from_before_the_layout_version_still_loads()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, """
            { "IsAdvanced": false, "AdvancedIsOn": false, "Filters": [], "IsOn": true, "Mode": "Amber", "Level": 30,
              "ScheduleEnabled": true, "ScheduleStart": "22:00:00", "ScheduleEnd": "06:00:00", "TransitionSeconds": 600 }
            """);

        var loaded = Settings.Load(SettingsPath);

        Assert.Equal(0, loaded.SettingsVersion);
        Assert.Null(loaded.SkippedVersion);
        Assert.Null(loaded.NotesShownVersion);
        Assert.True(loaded.IsOn);
        Assert.Equal(TintMode.Amber, loaded.Mode);
        Assert.Equal(30, loaded.Level);
        Assert.Equal(new TimeOnly(22, 0), loaded.ScheduleStart);
        Assert.Equal(600, loaded.TransitionSeconds);
    }

    [Fact]
    public void Missing_file_gives_defaults()
    {
        AssertDefaults(Settings.Load(SettingsPath));
    }

    [Fact]
    public void Corrupt_file_gives_defaults()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, "{ not json");

        AssertDefaults(Settings.Load(SettingsPath));
    }

    [Fact]
    public void Change_is_announced_only_when_the_value_differs()
    {
        var settings = new Settings();
        var changed = new List<string?>();
        settings.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        settings.Level = 50; // same as default
        settings.Level = 60;
        settings.IsOn = true;

        Assert.Equal([nameof(Settings.Level), nameof(Settings.IsOn)], changed);
    }

    static void AssertDefaults(Settings settings)
    {
        Assert.False(settings.IsOn);
        Assert.Equal(TintMode.GoldenHour, settings.Mode);
        Assert.Equal(50, settings.Level);
        Assert.False(settings.ScheduleEnabled);
        Assert.Equal(new TimeOnly(12, 0), settings.ScheduleStart);
        Assert.Equal(new TimeOnly(13, 0), settings.ScheduleEnd);
        Assert.False(settings.IsAdvanced);
        Assert.Equal(0, settings.TransitionSeconds);
        Assert.False(settings.AdvancedIntroClosed);
        Assert.False(settings.FilterHintDone);
    }
}
