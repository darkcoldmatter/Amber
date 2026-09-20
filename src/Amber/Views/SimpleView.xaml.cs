using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Amber.Tint;

namespace Amber.Views;

public partial class SimpleView : UserControl
{
    const double ScheduleHeight = 403, PlainHeight = 263;
    static readonly TintMode[] Modes = Enum.GetValues<TintMode>();
    static readonly Duration Crossfade = new(TimeSpan.FromSeconds(0.35));

    readonly Settings _settings;
    bool _filling;

    public SimpleView(Settings settings)
    {
        InitializeComponent();
        _settings = settings;
        DataContext = settings;

        StartHour.ItemsSource = TimeOptions.Hours;
        EndHour.ItemsSource = TimeOptions.Hours;
        StartMinute.ItemsSource = TimeOptions.Minutes;
        EndMinute.ItemsSource = TimeOptions.Minutes;
        foreach (var box in new[] { StartHour, StartMinute, EndHour, EndMinute, Transition })
            box.SelectionChanged += OnTimeBoxChanged;

        HeroBack.Source = HeroFor(settings.Mode);
        ModeNameBack.Text = NameOf(settings.Mode);
        ApplyModeColors(settings.Mode);
        ShowSchedule();
        Root.Height = settings.ScheduleEnabled ? ScheduleHeight : PlainHeight;

        Level.PreviewStarted += (_, _) => App.Current.StartPreview();
        Level.PreviewEnded += (_, _) => App.Current.EndPreview();
        settings.PropertyChanged += OnSettingChanged;
        Unloaded += (_, _) => settings.PropertyChanged -= OnSettingChanged;
    }

    void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Settings.Mode):
                CrossfadeTo(_settings.Mode);
                ApplyModeColors(_settings.Mode);
                break;
            case nameof(Settings.ScheduleEnabled):
                Root.Height = _settings.ScheduleEnabled ? ScheduleHeight : PlainHeight;
                ApplyScheduleColor();
                ShowSchedule();
                break;
            case nameof(Settings.ScheduleStart):
            case nameof(Settings.ScheduleEnd):
                ShowSchedule();
                break;
        }
    }

    void OnPreviousMode(object sender, RoutedEventArgs e) => Step(-1);
    void OnNextMode(object sender, RoutedEventArgs e) => Step(1);

    void Step(int direction)
    {
        var next = (Array.IndexOf(Modes, _settings.Mode) + direction + Modes.Length) % Modes.Length;
        _settings.Mode = Modes[next];
    }

    /// <summary>Only the background and the mode name change, and they dissolve into the new one.</summary>
    void CrossfadeTo(TintMode mode)
    {
        HeroFront.Source = HeroFor(mode);
        ModeNameFront.Text = NameOf(mode);
        ModeNameFront.Foreground = LabelBrush(mode);

        var fade = new DoubleAnimation(0, 1, Crossfade);
        fade.Completed += (_, _) =>
        {
            HeroBack.Source = HeroFront.Source;
            ModeNameBack.Text = ModeNameFront.Text;
            ModeNameBack.Foreground = ModeNameFront.Foreground;
            HeroFront.BeginAnimation(OpacityProperty, null);
            ModeNameFront.BeginAnimation(OpacityProperty, null);
            HeroFront.Opacity = 0;
            ModeNameFront.Opacity = 0;
        };
        HeroFront.BeginAnimation(OpacityProperty, fade);
        ModeNameFront.BeginAnimation(OpacityProperty, fade);
    }

    static readonly Brush ScheduleOffBrush = new SolidColorBrush(Color.FromRgb(0x85, 0x81, 0x81));

    void ApplyModeColors(TintMode mode)
    {
        var accent = (Brush)FindResource(mode + "Accent");
        ModeNameBack.Foreground = LabelBrush(mode);
        Level.Foreground = accent;
        Bar.Accent = accent;
        ApplyScheduleColor();
    }

    /// <summary>The Schedule pill is grey until the schedule is on, then it takes the mode's color.</summary>
    void ApplyScheduleColor() =>
        ScheduleToggle.Foreground = _settings.ScheduleEnabled ? LabelBrush(_settings.Mode) : ScheduleOffBrush;

    Brush LabelBrush(TintMode mode) => (Brush)FindResource(mode + "Label");

    /// <summary>Fills the dropdowns from the settings, and keeps the transition list in step with the schedule length.</summary>
    void ShowSchedule()
    {
        _filling = true;

        StartHour.SelectedIndex = _settings.ScheduleStart.Hour;
        StartMinute.SelectedIndex = _settings.ScheduleStart.Minute / TimeOptions.ScheduleStep;
        EndHour.SelectedIndex = _settings.ScheduleEnd.Hour;
        EndMinute.SelectedIndex = _settings.ScheduleEnd.Minute / TimeOptions.ScheduleStep;

        var length = ScheduleRange.Length(ScheduleRange.Minutes(_settings.ScheduleStart), ScheduleRange.Minutes(_settings.ScheduleEnd));
        var choices = TimeOptions.Transitions(length);
        Transition.ItemsSource = choices;
        if (_settings.TransitionMinutes > choices[^1]) _settings.TransitionMinutes = choices[^1];
        Transition.SelectedItem = _settings.TransitionMinutes;

        _filling = false;
    }

    void OnTimeBoxChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_filling) return;

        _settings.ScheduleStart = TimeFrom(StartHour, StartMinute);
        _settings.ScheduleEnd = TimeFrom(EndHour, EndMinute);
        if (Transition.SelectedItem is int minutes) _settings.TransitionMinutes = minutes;
    }

    static TimeOnly TimeFrom(ComboBox hours, ComboBox minutes) =>
        new(Math.Max(hours.SelectedIndex, 0), Math.Max(minutes.SelectedIndex, 0) * TimeOptions.ScheduleStep);

    static string NameOf(TintMode mode) => mode switch
    {
        TintMode.GoldenHour => "Golden hour",
        TintMode.Amber => "Amber",
        _ => "Astronomic",
    };

    static BitmapImage HeroFor(TintMode mode)
    {
        var file = mode switch
        {
            TintMode.GoldenHour => "golden-hour",
            TintMode.Amber => "amber",
            _ => "astronomic",
        };
        return new BitmapImage(new Uri($"pack://application:,,,/Assets/Heroes/{file}.png"));
    }
}
