using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
#if DEBUG
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
#endif
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
#if DEBUG
    // The e-ink experiment's chips, after Custom (test builds only). While one is picked the slider and the big number
    // are its amber amount, kept in EInkExperiment and never in the settings.
    readonly Dictionary<EInkLook, RadioButton> _einkChips = [];
    bool _switchingSlider;
    static bool EInkPicked => EInkExperiment.Look is not null;
#else
    static bool EInkPicked => false;
#endif

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

#if DEBUG
        AddEInkChips();
        if (EInkPicked) ShowEInk();
        else
#endif
        _chips[settings.Mode].IsChecked = true;
        ShowLevel();
        ShowSchedule();

        Level.PreviewStarted += (_, _) => App.Current.StartPreview();
        Level.PreviewEnded += (_, _) => App.Current.EndPreview();
        Hue.PreviewStarted += (_, _) => App.Current.StartPreview();
        Hue.PreviewEnded += (_, _) => App.Current.EndPreview();
        settings.PropertyChanged += OnSettingChanged;
        Unloaded += (_, _) => settings.PropertyChanged -= OnSettingChanged;
#if DEBUG
        EInkExperiment.Changed += ShowEInk;
        Unloaded += (_, _) => EInkExperiment.Changed -= ShowEInk;
#endif
    }

#if DEBUG
    void AddEInkChips()
    {
        var style = (Style)FindResource("ModeChip");
        for (var k = 0; k < EInkLook.All.Length; k++)
        {
            var look = EInkLook.All[k];
            var chip = new RadioButton { Style = style, Content = $"E-ink {k + 1}", Margin = new Thickness(16, 0, 0, 0) };
            AutomationProperties.SetName(chip, $"E-ink {k + 1}");
            chip.Checked += (_, _) => EInkExperiment.Look = look;
            ChipRow.Children.Add(chip);
            _einkChips[look] = chip;
        }

        Level.ValueChanged += (_, e) =>
        {
            if (_switchingSlider || !EInkPicked) return;
            EInkExperiment.Amber = e.NewValue;
            ShowLevel();
        };
    }

    /// <summary>
    /// Picking an e-ink look (here or in the tray) ticks its chip and hands the slider to the amber amount: unbound from
    /// the level first, so nothing reaches the settings. Leaving it ticks the saved tint's chip and binds the slider back.
    /// </summary>
    void ShowEInk()
    {
        _switchingSlider = true;
        if (EInkExperiment.Look is { } look)
        {
            _einkChips[look].IsChecked = true;
            var amber = EInkExperiment.Amber;
            BindingOperations.ClearBinding(Level, RangeBase.ValueProperty);
            Level.Value = amber;
        }
        else
        {
            _chips[_settings.Mode].IsChecked = true;
            if (!BindingOperations.IsDataBound(Level, RangeBase.ValueProperty))
                Level.SetBinding(RangeBase.ValueProperty, new Binding(nameof(Settings.Level)));
        }
        _switchingSlider = false;
        ShowLevel();
        ShowLayout();
    }
#endif

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

    void OnModeChecked(object sender, RoutedEventArgs e)
    {
        _settings.Mode = _chips.First(chip => chip.Value == sender).Key;
#if DEBUG
        EInkExperiment.Look = null; // a real tint again
#endif
    }

    /// <summary>The big number follows the slider live, also while dragging.</summary>
    void ShowLevel()
    {
#if DEBUG
        if (EInkPicked)
        {
            Percent.Text = EInkExperiment.Amber.ToString("0", CultureInfo.InvariantCulture) + "%";
            return;
        }
#endif
        Percent.Text = _settings.Level.ToString("0", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>
    /// Custom's colour bar shows only while Custom is the tint, and the schedule rows only while the schedule is on; the
    /// window grows to fit them.
    /// </summary>
    void ShowLayout()
    {
        var custom = _settings.Mode == TintMode.Custom && !EInkPicked;
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
