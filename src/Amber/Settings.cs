using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amber.Tint;

namespace Amber;

/// <summary>User settings, saved as JSON. Raises PropertyChanged so the tint, tray icon and window stay in sync.</summary>
public sealed class Settings : INotifyPropertyChanged
{
    public static readonly string DefaultPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Amber", "settings.json");

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public Settings()
    {
        // Any change to a filter, or to the list itself, counts as a settings change.
        Filters.CollectionChanged += (_, e) =>
        {
            foreach (var filter in e.OldItems?.Cast<Filter>() ?? []) filter.PropertyChanged -= OnFilterChanged;
            foreach (var filter in e.NewItems?.Cast<Filter>() ?? []) filter.PropertyChanged += OnFilterChanged;
            Announce(nameof(Filters));
        };
    }

    /// <summary>Which app mode the window shows: simple (default) or advanced.</summary>
    public bool IsAdvanced { get; set => Set(ref field, value); }

    /// <summary>Advanced mode has its own on/off, and turning it on turns simple mode off.</summary>
    public bool AdvancedIsOn { get; set => Set(ref field, value); }

    /// <summary>Populate: the list is read-only, so the reader has to add into it rather than replace it.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public ObservableCollection<Filter> Filters { get; } = [];

    public bool IsOn { get; set => Set(ref field, value); }
    public TintMode Mode { get; set => Set(ref field, value); }
    public double Level { get; set => Set(ref field, value); } = 50;
    public bool ScheduleEnabled { get; set => Set(ref field, value); }
    public TimeOnly ScheduleStart { get; set => Set(ref field, value); } = new(20, 0);
    public TimeOnly ScheduleEnd { get; set => Set(ref field, value); } = new(8, 0);

    /// <summary>Schedule fade length in seconds; 0 ("Off") falls back to <see cref="Fade.DefaultSeconds"/>.</summary>
    public int TransitionSeconds { get; set => Set(ref field, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Returns default settings if the file is missing or unreadable.</summary>
    public static Settings Load(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), JsonOptions) ?? new();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    void OnFilterChanged(object? sender, PropertyChangedEventArgs e) => Announce(nameof(Filters));

    void Announce(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    void Set<T>(ref T storage, T value, [CallerMemberName] string name = "")
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return;
        storage = value;
        Announce(name);
    }
}
