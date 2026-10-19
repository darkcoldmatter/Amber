using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Amber.Controls;
using Amber.Tint;

namespace Amber.Views;

public partial class SimpleView : UserControl
{
    const double ScheduleHeight = 446, PlainHeight = 286, TransitionListWidth = 75;
    // Where the chips and the schedule rows sit (as in the XAML), and how far down Custom's colour bar moves them:
    // its 22 px and the 12 px gap above it.
    const double ChipsTop = 230, ScheduleTop = 282, HueRoom = 34;

    readonly Settings _settings;
    readonly Dictionary<TintMode, RadioButton> _chips;

    public SimpleView(Settings settings, Updates updates)
    {
        InitializeComponent();
        _settings = settings;
        DataContext = settings;
        Notice.Updates = updates;
        _chips = new()
        {
            [TintMode.GoldenHour] = GoldenHourChip,
            [TintMode.Amber] = AmberChip,
            [TintMode.DeepNight] = DeepNightChip,
            [TintMode.Custom] = CustomChip,
        };

        _chips[settings.Mode].IsChecked = true;
        ShowLevel();
        ShowSchedule();

        Level.PreviewStarted += (_, _) => App.Current.StartPreview();
        Level.PreviewEnded += (_, _) => App.Current.EndPreview();
        Hue.PreviewStarted += (_, _) => App.Current.StartPreview();
        Hue.PreviewEnded += (_, _) => App.Current.EndPreview();
        settings.PropertyChanged += OnSettingChanged;
        Unloaded += (_, _) => settings.PropertyChanged -= OnSettingChanged;
    }

    void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Settings.Mode):
                _chips[_settings.Mode].IsChecked = true;
                ShowLayout();
                break;
            case nameof(Settings.Level):
                ShowLevel();
                break;
            case nameof(Settings.ScheduleEnabled):
            case nameof(Settings.ScheduleStart):
            case nameof(Settings.ScheduleEnd):
            case nameof(Settings.TransitionSeconds):
                ShowSchedule();
                break;
        }
    }

    void OnModeChecked(object sender, RoutedEventArgs e) =>
        _settings.Mode = _chips.First(chip => chip.Value == sender).Key;

    /// <summary>The big number follows the slider live, also while dragging.</summary>
    void ShowLevel() => Percent.Text = _settings.Level.ToString("0", CultureInfo.InvariantCulture) + "%";

    /// <summary>
    /// Custom's colour bar shows only while Custom is the tint, and the schedule rows only while the schedule is on; the
    /// window grows to fit them.
    /// </summary>
    void ShowLayout()
    {
        var custom = _settings.Mode == TintMode.Custom;
        var room = custom ? HueRoom : 0;
        Hue.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        Canvas.SetTop(Chips, ChipsTop + room);
        Canvas.SetTop(SchedulePanel, ScheduleTop + room);
        SchedulePanel.Visibility = _settings.ScheduleEnabled ? Visibility.Visible : Visibility.Collapsed;
        Root.Height = (_settings.ScheduleEnabled ? ScheduleHeight : PlainHeight) + room;
    }

    /// <summary>
    /// The schedule rows show only while the schedule is on (<see cref="ShowLayout"/>).
    /// The transition can't be longer than the schedule allows, so a shorter schedule shortens it.
    /// </summary>
    void ShowSchedule()
    {
        ShowLayout();

        var choices = TransitionChoices();
        if (_settings.TransitionSeconds > choices[^1]) _settings.TransitionSeconds = choices[^1];
        Transition.Content = TimeOptions.TransitionLabel(_settings.TransitionSeconds);
    }

    IReadOnlyList<int> TransitionChoices() => TimeOptions.Transitions(
        ScheduleRange.Length(ScheduleRange.Minutes(_settings.ScheduleStart), ScheduleRange.Minutes(_settings.ScheduleEnd)));

    void OnTransitionClick(object sender, RoutedEventArgs e)
    {
        var choices = TransitionChoices();
        DropdownList.Open(Transition, choices.Select(TimeOptions.TransitionLabel).ToArray(),
            choices.ToList().IndexOf(_settings.TransitionSeconds), TransitionListWidth,
            index => _settings.TransitionSeconds = choices[index]);
    }
}
