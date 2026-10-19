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

    /// <summary>
    /// The layout of the file this Amber writes. A later Amber that changes the layout raises it, and upgrades older
    /// files as it loads them.
    /// </summary>
    public const int CurrentVersion = 1;

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

    /// <summary>
    /// The layout the loaded file was written in: <see cref="CurrentVersion"/> once saved, 0 for a file from before
    /// this number was written. First in the file.
    /// </summary>
    [JsonPropertyOrder(-1)]
    public int SettingsVersion { get; set; }

    /// <summary>Which app mode the window shows: simple (default) or advanced.</summary>
    public bool IsAdvanced { get; set => Set(ref field, value); }

    /// <summary>Advanced mode has its own on/off, and turning it on turns simple mode off.</summary>
    public bool AdvancedIsOn { get; set => Set(ref field, value); }

    /// <summary>The "How it works" card of advanced mode was closed with its ✕, so it stays away.</summary>
    public bool AdvancedIntroClosed { get; set => Set(ref field, value); }

    /// <summary>The "Click a filter to adjust it" bubble has done its job: a filter has been selected once.</summary>
    public bool FilterHintDone { get; set => Set(ref field, value); }

    /// <summary>Populate: the list is read-only, so the reader has to add into it rather than replace it.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public ObservableCollection<Filter> Filters { get; } = [];

    public bool IsOn { get; set => Set(ref field, value); }
    public TintMode Mode { get; set => Set(ref field, value); }
    public double Level { get; set => Set(ref field, value); } = 50;

    /// <summary>Custom's colour in simple mode, as a hue of 0–360 (red by default). Like the level, switching tints keeps it.</summary>
    public double CustomHue { get; set => Set(ref field, value); }

    public bool ScheduleEnabled { get; set => Set(ref field, value); }
    public TimeOnly ScheduleStart { get; set => Set(ref field, value); } = new(12, 0);
    public TimeOnly ScheduleEnd { get; set => Set(ref field, value); } = new(13, 0);

    /// <summary>Schedule fade length in seconds; 0 ("Off") falls back to <see cref="Fade.DefaultSeconds"/>.</summary>
    public int TransitionSeconds { get; set => Set(ref field, value); }

    /// <summary>The update version the user chose to skip, so it isn't announced again (<see cref="Updates.Skip"/>).</summary>
    public string? SkippedVersion { get; set => Set(ref field, value); }

    /// <summary>The version whose "New in this version" window was closed, so it shows once (<see cref="WhatsNew"/>).</summary>
    public string? NotesShownVersion { get; set => Set(ref field, value); }

    /// <summary>Whether <see cref="Load"/> found a settings file; false the first time Amber ever starts. Not saved.</summary>
    [JsonIgnore]
    public bool HadFile { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Returns default settings if the file is missing or unreadable.</summary>
    public static Settings Load(string path)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), JsonOptions) ?? new();
            settings.HadFile = true;
            // Older versions could save a curve that stops short of its range's ends: carry it on to them, same shape.
            foreach (var filter in settings.Filters)
                if (CurveMath.ReachEnds(filter.Curve, filter.LengthMinutes) is var curve && !curve.SequenceEqual(filter.Curve)) filter.Curve = curve;
            return settings;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // A file that is there but unreadable still means Amber has run here before.
            return new() { HadFile = e is not (FileNotFoundException or DirectoryNotFoundException) };
        }
    }

    public void Save(string path)
    {
        SettingsVersion = CurrentVersion;
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
