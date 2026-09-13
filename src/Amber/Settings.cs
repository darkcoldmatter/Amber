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

    public bool IsOn { get; set => Set(ref field, value); }
    public TintMode Mode { get; set => Set(ref field, value); }
    public double Level { get; set => Set(ref field, value); } = 50;
    public bool ScheduleEnabled { get; set => Set(ref field, value); }
    public TimeOnly ScheduleStart { get; set => Set(ref field, value); } = new(21, 0);
    public TimeOnly ScheduleEnd { get; set => Set(ref field, value); } = new(7, 0);

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

    void Set<T>(ref T storage, T value, [CallerMemberName] string name = "")
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
