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
        Assert.Equal(new TimeOnly(20, 0), settings.ScheduleStart);
        Assert.Equal(new TimeOnly(8, 0), settings.ScheduleEnd);
        Assert.False(settings.IsAdvanced);
        Assert.Equal(0, settings.TransitionSeconds);
    }
}
