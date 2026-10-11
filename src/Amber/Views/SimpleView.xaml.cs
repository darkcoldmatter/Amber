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

    readonly Settings _settings;
    readonly Dictionary<TintMode, RadioButton> _chips;

    public SimpleView(Settings settings)
    {
        InitializeComponent();
        _settings = settings;
        DataContext = settings;
        _chips = new()
        {
            [TintMode.GoldenHour] = GoldenHourChip,
            [TintMode.Amber] = AmberChip,
            [TintMode.DeepNight] = DeepNightChip,
        };

        _chips[settings.Mode].IsChecked = true;
        ShowLevel();
        ShowSchedule();

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
                _chips[_settings.Mode].IsChecked = true;
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
    /// The schedule rows show only while the schedule is on, and the window grows to fit them.
    /// The transition can't be longer than the schedule allows, so a shorter schedule shortens it.
    /// </summary>
    void ShowSchedule()
    {
        Root.Height = _settings.ScheduleEnabled ? ScheduleHeight : PlainHeight;
        SchedulePanel.Visibility = _settings.ScheduleEnabled ? Visibility.Visible : Visibility.Collapsed;

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
